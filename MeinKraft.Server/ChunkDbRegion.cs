using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;

/// <summary>
/// Region-file based <see cref="IChunkDb"/> implementation.
/// Replaces the SQLite backend with a format purpose-built for the voxel
/// chunk access pattern: random reads by position key, batched sequential
/// writes during generation, occasional single-chunk updates.
///
/// <b>Format overview</b>
/// <para>
/// Chunks are grouped into region files, each covering a fixed cube of chunk
/// space (<see cref="DefaultChunksPerAxis"/>³ slots).  At the default of 16
/// chunks per axis with a 16-block chunk size, one region file covers a
/// 256 × 256 × 256 block volume.
/// </para>
/// <para>
/// Each region file contains three sections, all aligned to
/// <see cref="SectorSize"/>-byte sectors:
/// </para>
/// <list type="table">
///   <item><term>Sector 0</term>
///         <description>Header — magic, version, geometry parameters</description></item>
///   <item><term>Sectors 1 .. N</term>
///         <description>Offset table — one 8-byte slot per chunk</description></item>
///   <item><term>Sectors N+1 ..</term>
///         <description>Chunk data — variable-length blobs on sector boundaries</description></item>
/// </list>
///
/// <b>Offset table slot (8 bytes, little-endian)</b>
/// <list type="table">
///   <item><term>[0-3] SectorOffset : uint32</term>
///         <description>Absolute sector index of the chunk data; 0 = empty slot</description></item>
///   <item><term>[4-5] SectorCount  : uint16</term>
///         <description>Number of consecutive sectors occupied (including length prefix)</description></item>
///   <item><term>[6-7] Flags        : uint16</term>
///         <description>Bit 0 = slot is occupied</description></item>
/// </list>
///
/// <b>Chunk data layout</b>
/// <code>
///   [0-3]  DataLength : int32    — compressed payload size in bytes
///   [4..]  Data       : byte[]   — DataLength bytes of payload
///   [..]   Padding    : zeros    — fills remainder of the last sector
/// </code>
///
/// <b>Performance properties</b>
/// <list type="bullet">
///   <item>No SQL parsing, no query planner — lookup is one array index + one seek.</item>
///   <item>Offset table lives entirely in memory (4 096 slots × 8 bytes = 32 KB for a 16³ region).</item>
///   <item>In-place sector reuse avoids file growth when compressed chunk size is stable.</item>
///   <item>Batch writes flush the offset table once per batch, not once per chunk.</item>
///   <item>Region files stay open for the session lifetime, amortising the O(N) table read across all subsequent accesses.</item>
/// </list>
/// </summary>
public sealed class ChunkDbRegion : IChunkDbRegion, IDisposable
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Number of chunks along each axis in one region file (default).
    /// 16³ = 4 096 chunk slots per file; each file covers
    /// (ChunksPerAxis × chunkSize)³ world blocks.
    /// </summary>
    public const int DefaultChunksPerAxis = 16;

    /// <summary>
    /// Bytes per sector — must equal the OS page size for optimal I/O.
    /// All offsets and lengths in the file are sector-aligned.
    /// </summary>
    public const int SectorSize = 4096;

    // ── State ─────────────────────────────────────────────────────────────────

    private string _directory = "";  // sibling directory holding region files
    private string _sentinelPath = "";  // the .mddbs file picked by FileOpenDialog
    private readonly int _chunksPerAxis;

    // Open region files keyed by packed region coordinate.
    // Kept open for the session lifetime to avoid re-reading the offset table.
    private readonly Dictionary<ulong, RegionFile> _openRegions = new();

    /// <inheritdoc/>
    public bool ReadOnly { get; set; }

    // In ReadOnly mode all writes go here; reads check here first.
    // Matches the behaviour of the original ChunkDbSqlite read-only path.
    private readonly Dictionary<ulong, byte[]> _temporaryChunks = new();

    private const string GlobalDataFilename = "global.dat";
    private const string RegionFileExt = ".rgn";

    // ── Construction ──────────────────────────────────────────────────────────

    /// <param name="chunksPerAxis">
    /// Chunks per axis per region file.  Must match the value used when the
    /// store was first created — validated against the header on open.
    /// </param>
    public ChunkDbRegion(int chunksPerAxis = DefaultChunksPerAxis)
    {
        _chunksPerAxis = chunksPerAxis;
    }

    // ── IChunkDb — lifecycle ──────────────────────────────────────────────────

    /// <summary>
    /// Opens or creates the region store identified by the
    /// <paramref name="sentinelPath"/> file (e.g. <c>MySave.mddbs</c>).
    /// <para>
    /// The actual region data lives in a sibling directory with the same name
    /// minus the extension (<c>MySave/</c>).  The sentinel file is what the
    /// existing <c>FileOpenDialog("mddbs", …)</c> call presents to the player —
    /// preserving the original single-file UX without packing data into one file.
    /// </para>
    /// </summary>
    public void Open(string sentinelPath)
    {
        _sentinelPath = sentinelPath;
        // "saves/MySave.mddbs" → "saves/MySave/"
        _directory = Path.ChangeExtension(sentinelPath, null);
        Directory.CreateDirectory(_directory);

        // Create the sentinel if this is a brand-new save.
        if (!File.Exists(_sentinelPath))
            File.WriteAllBytes(_sentinelPath, []);
    }

    /// <summary>
    /// Copies the sentinel file and all region files to
    /// <paramref name="backupSentinelPath"/> and its sibling directory.
    /// </summary>
    public void Backup(string backupSentinelPath)
    {
        string backupDir = Path.ChangeExtension(backupSentinelPath, null);
        Directory.CreateDirectory(backupDir);

        // Copy the sentinel file.
        File.Copy(_sentinelPath, backupSentinelPath, overwrite: true);

        // Copy all region and global-data files.
        foreach (string file in Directory.EnumerateFiles(_directory))
        {
            File.Copy(file,
                      Path.Combine(backupDir, Path.GetFileName(file)),
                      overwrite: true);
        }
    }

    // ── IChunkDb — read ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IEnumerable<byte[]> GetChunks(IEnumerable<Vector3i> positions)
    {
        foreach (Vector3i pos in positions)
        {
            ulong chunkKey = PackChunkKey(pos.X, pos.Y, pos.Z);

            // ReadOnly: check in-memory write buffer first.
            if (ReadOnly && _temporaryChunks.TryGetValue(chunkKey, out byte[] cached))
            {
                yield return cached;
                continue;
            }

            ChunkToRegion(pos, out int rx, out int ry, out int rz, out int localIdx);
            yield return GetOrOpenRegion(rx, ry, rz, _directory).ReadChunk(localIdx);
        }
    }

    /// <inheritdoc/>
    public byte[] GetGlobalData()
    {
        string path = Path.Combine(_directory, GlobalDataFilename);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <inheritdoc/>
    public Dictionary<Vector3i, byte[]> GetChunksFromFile(
        IEnumerable<Vector3i> positions, string sourceSentinelPath)
    {
        // Derive the data directory from the sentinel path, same as Open().
        string sourceDirectory = Path.ChangeExtension(sourceSentinelPath, null);
        if (!Directory.Exists(sourceDirectory))
        {
            Console.WriteLine($"[ChunkDbRegion] Source directory '{sourceDirectory}' does not exist.");
            return [];
        }

        Dictionary<Vector3i, byte[]> result = new();

        // Use a local region cache so foreign-directory files never pollute _openRegions.
        Dictionary<ulong, RegionFile> localRegions = new();
        try
        {
            foreach (Vector3i pos in positions)
            {
                ChunkToRegion(pos, out int rx, out int ry, out int rz, out int localIdx);
                ulong rk = PackRegionKey(rx, ry, rz);
                if (!localRegions.TryGetValue(rk, out RegionFile rf))
                {
                    string regionPath = BuildRegionPath(rx, ry, rz, sourceDirectory);
                    if (!File.Exists(regionPath))
                    {
                        result[pos] = null;
                        continue;
                    }
                    rf = RegionFile.Open(regionPath, _chunksPerAxis);
                    localRegions[rk] = rf;
                }
                result[pos] = rf.ReadChunk(localIdx);
            }
        }
        finally
        {
            foreach (RegionFile rf in localRegions.Values) rf.Dispose();
        }

        return result;
    }

    // ── IChunkDb — write ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetChunks(IEnumerable<DbChunk> chunks)
    {
        if (ReadOnly)
        {
            foreach (DbChunk c in chunks)
                _temporaryChunks[PackChunkKey(c.Position.X, c.Position.Y, c.Position.Z)]
                    = (byte[])c.Chunk.Clone();
            return;
        }

        // Group writes by region so each region's offset table is flushed once
        // for the entire batch, not once per chunk.
        HashSet<RegionFile> dirtyRegions = new();
        foreach (DbChunk c in chunks)
        {
            ChunkToRegion(c.Position, out int rx, out int ry, out int rz, out int localIdx);
            RegionFile rf = GetOrOpenRegion(rx, ry, rz, _directory);
            rf.WriteChunk(localIdx, c.Chunk);
            dirtyRegions.Add(rf);
        }

        foreach (RegionFile rf in dirtyRegions)
            rf.Flush();
    }

    /// <inheritdoc/>
    public void SetGlobalData(byte[] data)
    {
        string path = Path.Combine(_directory, GlobalDataFilename);
        if (data == null) { if (File.Exists(path)) File.Delete(path); return; }
        File.WriteAllBytes(path, data);
    }

    /// <inheritdoc/>
    public void SetChunksToFile(IEnumerable<DbChunk> chunks, string destSentinelPath)
    {
        if (Path.GetFullPath(_sentinelPath) == Path.GetFullPath(destSentinelPath))
        {
            Console.WriteLine("[ChunkDbRegion] Cannot write to the currently open store.");
            return;
        }

        // Mirror Open(): create the sentinel file and its sibling data directory.
        string destDirectory = Path.ChangeExtension(destSentinelPath, null);
        Directory.CreateDirectory(destDirectory);
        if (!File.Exists(destSentinelPath))
            File.WriteAllBytes(destSentinelPath, []);

        Dictionary<ulong, RegionFile> localRegions = new();
        try
        {
            foreach (DbChunk c in chunks)
            {
                ChunkToRegion(c.Position, out int rx, out int ry, out int rz, out int localIdx);
                ulong rk = PackRegionKey(rx, ry, rz);
                if (!localRegions.TryGetValue(rk, out RegionFile rf))
                {
                    rf = RegionFile.Open(BuildRegionPath(rx, ry, rz, destDirectory), _chunksPerAxis);
                    localRegions[rk] = rf;
                }
                rf.WriteChunk(localIdx, c.Chunk);
            }
        }
        finally
        {
            foreach (RegionFile rf in localRegions.Values)
            {
                rf.Flush();
                rf.Dispose();
            }
        }
    }

    // ── IChunkDb — delete ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void DeleteChunks(IEnumerable<Vector3i> positions)
    {
        if (ReadOnly)
        {
            foreach (Vector3i pos in positions)
                _temporaryChunks.Remove(PackChunkKey(pos.X, pos.Y, pos.Z));
            return;
        }

        HashSet<RegionFile> dirtyRegions = new();
        foreach (Vector3i pos in positions)
        {
            ChunkToRegion(pos, out int rx, out int ry, out int rz, out int localIdx);
            RegionFile rf = GetOrOpenRegion(rx, ry, rz, _directory);
            rf.DeleteChunk(localIdx);
            dirtyRegions.Add(rf);
        }

        foreach (RegionFile rf in dirtyRegions)
            rf.Flush();
    }

    // ── IChunkDb — temporary store ────────────────────────────────────────────

    /// <summary>
    /// Discards all writes accumulated in the in-memory temporary store
    /// while <see cref="ReadOnly"/> was active.
    /// Has no effect on data already persisted to region files.
    /// </summary>
    public void ClearTemporaryChunks() => _temporaryChunks.Clear();

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>
    /// Flushes and closes all open region files.
    /// Must be called before the process exits to ensure offset tables are persisted.
    /// </summary>
    public void Dispose()
    {
        foreach (RegionFile rf in _openRegions.Values)
            rf.Dispose();
        _openRegions.Clear();
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Decomposes a chunk-space position into region coordinates plus a local
    /// slot index within the region's offset table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChunkToRegion(Vector3i pos,
        out int rx, out int ry, out int rz, out int localIdx)
    {
        rx = pos.X / _chunksPerAxis;
        ry = pos.Y / _chunksPerAxis;
        rz = pos.Z / _chunksPerAxis;
        int lx = pos.X - rx * _chunksPerAxis;
        int ly = pos.Y - ry * _chunksPerAxis;
        int lz = pos.Z - rz * _chunksPerAxis;
        localIdx = lx + ly * _chunksPerAxis + lz * (_chunksPerAxis * _chunksPerAxis);
    }

    private RegionFile GetOrOpenRegion(int rx, int ry, int rz, string directory)
    {
        ulong key = PackRegionKey(rx, ry, rz);
        if (!_openRegions.TryGetValue(key, out RegionFile rf))
        {
            rf = RegionFile.Open(BuildRegionPath(rx, ry, rz, directory), _chunksPerAxis);
            _openRegions[key] = rf;
        }
        return rf;
    }

    private static string BuildRegionPath(int rx, int ry, int rz, string directory)
        => Path.Combine(directory, $"r.{rx}.{ry}.{rz}{RegionFileExt}");

    /// <summary>
    /// Packs region coordinates into a <see langword="ulong"/> cache key.
    /// 20 bits per axis — supports up to 2²⁰ regions per axis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong PackRegionKey(int rx, int ry, int rz)
        => ((ulong)(uint)rx << 40) | ((ulong)(uint)ry << 20) | (uint)rz;

    /// <summary>
    /// Packs chunk-space coordinates into a <see langword="ulong"/> key for
    /// the ReadOnly temporary store.  Matches the bit layout of
    /// <c>ChunkDbSqlite.ToMapPos</c> for drop-in compatibility with
    /// existing <c>ChunkDbHelper</c> callers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong PackChunkKey(int x, int y, int z)
        => ((ulong)(uint)x << 40) | ((ulong)(uint)y << 20) | (uint)z;

    // =========================================================================
    //  RegionFile — manages one .rgn file on disk
    // =========================================================================

    /// <summary>
    /// Manages a single region file.  Keeps the offset table in memory and
    /// flushes it to disk explicitly after writes.
    ///
    /// Thread safety: not thread-safe.  All access must be serialised by the
    /// caller (matching the existing codebase convention).
    /// </summary>
    private sealed class RegionFile : IDisposable
    {
        // ── Format constants ──────────────────────────────────────────────────

        // "RGNS" — identifies the file as a region store
        private static ReadOnlySpan<byte> Magic => [0x52, 0x47, 0x4E, 0x53];
        private const byte FormatVersion = 1;

        // Each offset-table slot is 8 bytes:
        //   [0-3] SectorOffset uint32
        //   [4-5] SectorCount  uint16
        //   [6-7] Flags        uint16
        private const int SlotBytes = 8;

        // Shared zero buffer for sector padding — never written to by callers.
        private static readonly byte[] ZeroPad = new byte[SectorSize];

        // ── Instance state ────────────────────────────────────────────────────

        private readonly FileStream _stream;
        private readonly int _chunkCount;           // total slots in this region
        private readonly SlotEntry[] _slots;        // in-memory offset table
        private readonly uint _firstDataSector;     // sector index where blobs begin

        // Free-sector list: sectorStart → consecutiveSectorCount.
        // Rebuilt from the offset table on Open(); never persisted separately.
        private readonly SortedList<uint, uint> _freeSectors = new();

        private bool _tableDirty;

        // ── Construction ──────────────────────────────────────────────────────

        private RegionFile(
            FileStream stream, int chunkCount, uint firstDataSector, SlotEntry[] slots)
        {
            _stream = stream;
            _chunkCount = chunkCount;
            _firstDataSector = firstDataSector;
            _slots = slots;
        }

        /// <summary>
        /// Opens an existing region file or creates a new one at
        /// <paramref name="path"/>.  The offset table is read into memory
        /// and the free-sector list is rebuilt from it.
        /// </summary>
        public static RegionFile Open(string path, int chunksPerAxis)
        {
            int chunkCount = chunksPerAxis * chunksPerAxis * chunksPerAxis;
            int tableBytes = chunkCount * SlotBytes;
            int tableSectors = (tableBytes + SectorSize - 1) / SectorSize;
            uint firstData = (uint)(1 + tableSectors); // sector 0 = header

            bool isNew = !File.Exists(path);

            // RandomAccess: hint to the OS not to pre-fetch sequentially.
            // bufferSize=SectorSize: stream buffer matches one sector.
            var stream = new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None, bufferSize: SectorSize, FileOptions.RandomAccess);

            var slots = new SlotEntry[chunkCount];

            if (isNew)
            {
                WriteNewHeader(stream, chunksPerAxis, tableSectors);
            }
            else
            {
                ValidateHeader(stream, chunksPerAxis, path);
                ReadOffsetTable(stream, slots, chunkCount, tableSectors);
            }

            var rf = new RegionFile(stream, chunkCount, firstData, slots);
            rf.BuildFreeList();
            return rf;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the compressed blob stored at <paramref name="localIndex"/>,
        /// or <see langword="null"/> if the slot is empty.
        /// </summary>
        public byte[] ReadChunk(int localIndex)
        {
            ref readonly SlotEntry slot = ref _slots[localIndex];
            if (!slot.HasData) return null;

            _stream.Seek((long)slot.SectorOffset * SectorSize, SeekOrigin.Begin);

            Span<byte> lenBuf = stackalloc byte[4];
            _stream.ReadExactly(lenBuf);
            int length = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);

            byte[] data = new byte[length];
            _stream.ReadExactly(data);
            return data;
        }

        /// <summary>
        /// Writes <paramref name="data"/> to slot <paramref name="localIndex"/>.
        /// Reuses the existing sector allocation when the new payload fits;
        /// otherwise frees the old sectors and allocates a new run.
        /// </summary>
        public void WriteChunk(int localIndex, byte[] data)
        {
            ref SlotEntry slot = ref _slots[localIndex];
            int sectorsNeeded = SectorsRequired(4 + data.Length);

            uint sectorStart;
            if (slot.HasData)
            {
                if (slot.SectorCount >= sectorsNeeded)
                {
                    // Fits in existing allocation — reuse the start sector.
                    // Return any excess sectors to the free list.
                    sectorStart = slot.SectorOffset;
                    if (slot.SectorCount > sectorsNeeded)
                        FreeSectors(sectorStart + (uint)sectorsNeeded,
                                    slot.SectorCount - (uint)sectorsNeeded);
                }
                else
                {
                    // New payload is larger — release old sectors, allocate fresh.
                    FreeSectors(slot.SectorOffset, slot.SectorCount);
                    sectorStart = AllocateSectors(sectorsNeeded);
                }
            }
            else
            {
                sectorStart = AllocateSectors(sectorsNeeded);
            }

            // Write length prefix + payload.
            _stream.Seek((long)sectorStart * SectorSize, SeekOrigin.Begin);
            Span<byte> lenBuf = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lenBuf, data.Length);
            _stream.Write(lenBuf);
            _stream.Write(data);

            // Zero-pad to the next sector boundary.
            // padBytes is always < SectorSize, so ZeroPad is always large enough.
            int padBytes = sectorsNeeded * SectorSize - 4 - data.Length;
            if (padBytes > 0)
                _stream.Write(ZeroPad, 0, padBytes);

            // Update in-memory slot.
            _slots[localIndex] = new SlotEntry
            {
                SectorOffset = sectorStart,
                SectorCount = (ushort)sectorsNeeded,
                Flags = 1, // HasData
            };
            _tableDirty = true;
        }

        /// <summary>
        /// Marks slot <paramref name="localIndex"/> as empty and returns its
        /// sectors to the free list.  No-op if the slot is already empty.
        /// </summary>
        public void DeleteChunk(int localIndex)
        {
            ref SlotEntry slot = ref _slots[localIndex];
            if (!slot.HasData) return;
            FreeSectors(slot.SectorOffset, slot.SectorCount);
            _slots[localIndex] = default;
            _tableDirty = true;
        }

        /// <summary>
        /// Writes the in-memory offset table to disk if it has been modified.
        /// Called once after each <c>SetChunks</c> batch.
        /// </summary>
        public void Flush()
        {
            if (!_tableDirty) return;
            WriteOffsetTable();
        }

        public void Dispose()
        {
            Flush();
            _stream.Flush(flushToDisk: true);
            _stream.Dispose();
        }

        // ── Sector allocation ─────────────────────────────────────────────────

        /// <summary>
        /// Finds the first free run of ≥ <paramref name="count"/> consecutive
        /// sectors (first-fit), splits it if larger, and returns the starting
        /// sector index.  Extends the file if no suitable run exists.
        /// </summary>
        private uint AllocateSectors(int count)
        {
            for (int i = 0; i < _freeSectors.Count; i++)
            {
                uint start = _freeSectors.Keys[i];
                uint avail = _freeSectors.Values[i];
                if (avail < count) continue;

                _freeSectors.RemoveAt(i);
                // Return the tail of the run if it's larger than needed.
                if (avail > count)
                    _freeSectors[start + (uint)count] = avail - (uint)count;
                return start;
            }

            // No suitable run — extend the file and return the new tail.
            uint newStart = (uint)(_stream.Length / SectorSize);
            _stream.SetLength(_stream.Length + (long)count * SectorSize);
            return newStart;
        }

        /// <summary>
        /// Returns a sector run to the free list and merges it with any
        /// immediately adjacent free runs to prevent long-term fragmentation.
        /// </summary>
        private void FreeSectors(uint start, uint count)
        {
            uint mergedStart = start;
            uint mergedCount = count;

            // Check for an immediately preceding free run.
            // SortedList keys are ascending; scan from the right to find the
            // largest key that is still less than mergedStart.
            for (int i = _freeSectors.Count - 1; i >= 0; i--)
            {
                uint pStart = _freeSectors.Keys[i];
                if (pStart >= mergedStart) continue;
                if (pStart + _freeSectors.Values[i] == mergedStart)
                {
                    // Adjacent — absorb the preceding run.
                    mergedCount += _freeSectors.Values[i];
                    mergedStart = pStart;
                    _freeSectors.RemoveAt(i);
                }
                break; // only one candidate can exist
            }

            // Check for an immediately following free run.
            uint tail = mergedStart + mergedCount;
            if (_freeSectors.TryGetValue(tail, out uint nextCount))
            {
                _freeSectors.Remove(tail);
                mergedCount += nextCount;
            }

            _freeSectors[mergedStart] = mergedCount;
        }

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the free-sector list by scanning the in-memory offset table.
        /// Called once after <see cref="Open"/>; O(totalSectors) in the file size.
        /// </summary>
        private void BuildFreeList()
        {
            _freeSectors.Clear();

            uint totalSectors = (uint)(_stream.Length / SectorSize);
            if (totalSectors <= _firstDataSector) return;

            uint dataCount = totalSectors - _firstDataSector;

            // Mark sectors that are occupied by existing chunk data.
            bool[] occupied = new bool[dataCount];
            foreach (SlotEntry slot in _slots)
            {
                if (!slot.HasData) continue;
                for (uint s = slot.SectorOffset; s < slot.SectorOffset + slot.SectorCount; s++)
                {
                    // Guard against out-of-range indices in corrupt files.
                    uint rel = s - _firstDataSector;
                    if (rel < dataCount) occupied[rel] = true;
                }
            }

            // Collect contiguous free runs and add to the free list.
            uint runStart = 0;
            bool inRun = false;
            for (uint i = 0; i < dataCount; i++)
            {
                if (!occupied[i] && !inRun)
                {
                    runStart = _firstDataSector + i;
                    inRun = true;
                }
                else if (occupied[i] && inRun)
                {
                    _freeSectors[runStart] = _firstDataSector + i - runStart;
                    inRun = false;
                }
            }

            // Close any run that extends to the end of the file.
            if (inRun) _freeSectors[runStart] = totalSectors - runStart;
        }

        // ── Disk I/O ──────────────────────────────────────────────────────────

        private static void WriteNewHeader(FileStream stream, int chunksPerAxis, int tableSectors)
        {
            // Sector 0 — header.
            Span<byte> header = stackalloc byte[SectorSize];
            header.Clear();
            Magic.CopyTo(header);
            header[4] = FormatVersion;
            BinaryPrimitives.WriteInt32LittleEndian(header[5..], chunksPerAxis);
            BinaryPrimitives.WriteInt32LittleEndian(header[9..], SectorSize);
            stream.Write(header);

            // Sectors 1..N — zero-initialised offset table (all slots empty).
            byte[] emptyTable = new byte[tableSectors * SectorSize];
            stream.Write(emptyTable);
            stream.Flush();
        }

        private static void ValidateHeader(FileStream stream, int chunksPerAxis, string path)
        {
            Span<byte> header = stackalloc byte[SectorSize];
            stream.ReadExactly(header);

            if (!header[..4].SequenceEqual(Magic))
                throw new InvalidDataException(
                    $"[ChunkDbRegion] Bad magic bytes in '{path}'. File may be corrupt.");

            if (header[4] != FormatVersion)
                throw new InvalidDataException(
                    $"[ChunkDbRegion] Unsupported format version {header[4]} in '{path}'. Expected {FormatVersion}.");

            int storedCpa = BinaryPrimitives.ReadInt32LittleEndian(header[5..]);
            if (storedCpa != chunksPerAxis)
                throw new InvalidDataException(
                    $"[ChunkDbRegion] ChunksPerAxis mismatch in '{path}': file has {storedCpa}, store configured for {chunksPerAxis}.");
        }

        private static void ReadOffsetTable(
            FileStream stream, SlotEntry[] slots, int chunkCount, int tableSectors)
        {
            byte[] tableData = new byte[tableSectors * SectorSize];
            stream.ReadExactly(tableData);

            for (int i = 0; i < chunkCount; i++)
            {
                int off = i * SlotBytes;
                slots[i] = new SlotEntry
                {
                    SectorOffset = BinaryPrimitives.ReadUInt32LittleEndian(tableData.AsSpan(off)),
                    SectorCount = BinaryPrimitives.ReadUInt16LittleEndian(tableData.AsSpan(off + 4)),
                    Flags = BinaryPrimitives.ReadUInt16LittleEndian(tableData.AsSpan(off + 6)),
                };
            }
        }

        private void WriteOffsetTable()
        {
            // Write exactly chunkCount × SlotBytes bytes — the trailing bytes in
            // the last table sector retain their zero-init values and are never read.
            byte[] table = new byte[_chunkCount * SlotBytes];
            for (int i = 0; i < _chunkCount; i++)
            {
                int off = i * SlotBytes;
                BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(off), _slots[i].SectorOffset);
                BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(off + 4), _slots[i].SectorCount);
                BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(off + 6), _slots[i].Flags);
            }

            _stream.Seek(SectorSize, SeekOrigin.Begin); // skip header sector
            _stream.Write(table);
            _tableDirty = false;
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SectorsRequired(int bytes)
            => (bytes + SectorSize - 1) / SectorSize;
    }

    // =========================================================================
    //  SlotEntry — in-memory representation of an offset table entry
    // =========================================================================

    private struct SlotEntry
    {
        /// <summary>Absolute sector index of the chunk data in the file. 0 = empty slot.</summary>
        public uint SectorOffset;

        /// <summary>Number of consecutive sectors occupied by this chunk, including the length prefix.</summary>
        public ushort SectorCount;

        /// <summary>Flags: bit 0 = slot is occupied.</summary>
        public ushort Flags;

        public bool HasData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & 1) != 0;
        }
    }
}
