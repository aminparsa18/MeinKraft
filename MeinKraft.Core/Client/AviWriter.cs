using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// http://www.adp-gmbh.ch/csharp/avi/write_avi.html

public class AviWriter
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct AVISTREAMINFOW
    {
        public uint fccType, fccHandler, dwFlags, dwCaps;

        public ushort wPriority, wLanguage;

        public uint dwScale, dwRate,
                         dwStart, dwLength, dwInitialFrames, dwSuggestedBufferSize,
                         dwQuality, dwSampleSize, rect_left, rect_top,
                         rect_right, rect_bottom, dwEditCount, dwFormatChangeCount;

        public ushort szName0, szName1, szName2, szName3, szName4, szName5,
                         szName6, szName7, szName8, szName9, szName10, szName11,
                         szName12, szName13, szName14, szName15, szName16, szName17,
                         szName18, szName19, szName20, szName21, szName22, szName23,
                         szName24, szName25, szName26, szName27, szName28, szName29,
                         szName30, szName31, szName32, szName33, szName34, szName35,
                         szName36, szName37, szName38, szName39, szName40, szName41,
                         szName42, szName43, szName44, szName45, szName46, szName47,
                         szName48, szName49, szName50, szName51, szName52, szName53,
                         szName54, szName55, szName56, szName57, szName58, szName59,
                         szName60, szName61, szName62, szName63;
    }
    // vfw.h
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct AVICOMPRESSOPTIONS
    {
        public uint fccType;
        public uint fccHandler;
        public uint dwKeyFrameEvery;  // only used with AVICOMRPESSF_KEYFRAMES
        public uint dwQuality;
        public uint dwBytesPerSecond; // only used with AVICOMPRESSF_DATARATE
        public uint dwFlags;
        public IntPtr lpFormat;
        public uint cbFormat;
        public IntPtr lpParms;
        public uint cbParms;
        public uint dwInterleaveEvery;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    public class AviException : ApplicationException
    {
        public AviException(string s) : base(s) { }
        public AviException(string s, int hr)
            : base(s)
        {

            if (hr == AVIERR_BADPARAM)
            {
                err_msg = "AVIERR_BADPARAM";
            }
            else
            {
                err_msg = "unknown";
            }
        }

        public string ErrMsg() => err_msg;
        private const int AVIERR_BADPARAM = -2147205018;
        private readonly string err_msg;
    }

    public Bitmap Open(string fileName, uint frameRate, int width, int height)
    {
        frameRate_ = frameRate;
        width_ = (uint)width;
        height_ = (uint)height;
        bmp_ = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        System.Drawing.Imaging.BitmapData bmpDat = bmp_.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        stride_ = (uint)bmpDat.Stride;
        bmp_.UnlockBits(bmpDat);
        AVIFileInit();
        int hr = AVIFileOpenW(ref pfile_, fileName, 4097 /* OF_WRITE | OF_CREATE (winbase.h) */, 0);
        if (hr != 0)
        {
            throw new AviException("error for AVIFileOpenW");
        }

        CreateStream();
        SetOptions();

        return bmp_;
    }

    public void AddFrame()
    {

        System.Drawing.Imaging.BitmapData bmpDat = bmp_.LockBits(
          new Rectangle(0, 0, (int)width_, (int)height_), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        int hr = AVIStreamWrite(psCompressed_, count_, 1,
           bmpDat.Scan0, // pointer to data
           (int)(stride_ * height_),
           0, // 16 = AVIIF_KEYFRAMe
           0,
           0);

        if (hr != 0)
        {
            throw new AviException("AVIStreamWrite");
        }

        bmp_.UnlockBits(bmpDat);

        count_++;
    }

    public void Close()
    {
        AVIStreamRelease(ps_);
        AVIStreamRelease(psCompressed_);

        AVIFileRelease(pfile_);
        AVIFileExit();
    }

    private void CreateStream()
    {
        AVISTREAMINFOW strhdr = new()
        {
            fccType = fccType_,
            fccHandler = fccHandler_,
            dwFlags = 0,
            dwCaps = 0,
            wPriority = 0,
            wLanguage = 0,
            dwScale = 1,
            dwRate = frameRate_, // Frames per Second
            dwStart = 0,
            dwLength = 0,
            dwInitialFrames = 0,
            dwSuggestedBufferSize = height_ * stride_,
            dwQuality = 0xffffffff, //-1;         // Use default
            dwSampleSize = 0,
            rect_top = 0,
            rect_left = 0,
            rect_bottom = height_,
            rect_right = width_,
            dwEditCount = 0,
            dwFormatChangeCount = 0,
            szName0 = 0,
            szName1 = 0
        };

        int hr = AVIFileCreateStream(pfile_, out ps_, ref strhdr);

        if (hr != 0)
        {
            throw new AviException("AVIFileCreateStream");
        }
    }

    unsafe private void SetOptions()
    {
        AVICOMPRESSOPTIONS opts = new()
        {
            fccType = 0, //fccType_;
            fccHandler = 0,//fccHandler_;
            dwKeyFrameEvery = 0,
            dwQuality = 0,  // 0 .. 10000
            dwFlags = 0,  // AVICOMRPESSF_KEYFRAMES = 4
            dwBytesPerSecond = 0,
            lpFormat = new IntPtr(0),
            cbFormat = 0,
            lpParms = new IntPtr(0),
            cbParms = 0,
            dwInterleaveEvery = 0
        };

        AVICOMPRESSOPTIONS* p = &opts;
        AVICOMPRESSOPTIONS** pp = &p;

        IntPtr x = ps_;
        IntPtr* ptr_ps = &x;

        AVISaveOptions(0, 0, 1, ptr_ps, pp);

        // TODO: AVISaveOptionsFree(...)

        int hr = AVIMakeCompressedStream(out psCompressed_, ps_, ref opts, 0);
        if (hr != 0)
        {
            throw new AviException("AVIMakeCompressedStream");
        }

        BITMAPINFOHEADER bi = new()
        {
            biSize = 40,
            biWidth = (int)width_,
            biHeight = (int)height_,
            biPlanes = 1,
            biBitCount = 24,
            biCompression = 0,  // 0 = BI_RGB
            biSizeImage = stride_ * height_,
            biXPelsPerMeter = 0,
            biYPelsPerMeter = 0,
            biClrUsed = 0,
            biClrImportant = 0
        };

        hr = AVIStreamSetFormat(psCompressed_, 0, ref bi, 40);
        if (hr != 0)
        {
            throw new AviException("AVIStreamSetFormat", hr);
        }
    }

    [DllImport("avifil32.dll")]
    private static extern void AVIFileInit();

    [DllImport("avifil32.dll")]
    private static extern int AVIFileOpenW(ref int ptr_pfile, [MarshalAs(UnmanagedType.LPWStr)] string fileName, int flags, int dummy);

    [DllImport("avifil32.dll")]
    private static extern int AVIFileCreateStream(
      int ptr_pfile, out IntPtr ptr_ptr_avi, ref AVISTREAMINFOW ptr_streaminfo);

    [DllImport("avifil32.dll")]
    private static extern int AVIMakeCompressedStream(
      out IntPtr ppsCompressed, IntPtr aviStream, ref AVICOMPRESSOPTIONS ao, int dummy);

    [DllImport("avifil32.dll")]
    private static extern int AVIStreamSetFormat(
      IntPtr aviStream, int lPos, ref BITMAPINFOHEADER lpFormat, int cbFormat);

    [DllImport("avifil32.dll")]
    unsafe private static extern int AVISaveOptions(
      int hwnd, uint flags, int nStreams, IntPtr* ptr_ptr_avi, AVICOMPRESSOPTIONS** ao);

    [DllImport("avifil32.dll")]
    private static extern int AVIStreamWrite(
      IntPtr aviStream, int lStart, int lSamples, IntPtr lpBuffer,
      int cbBuffer, int dwFlags, int dummy1, int dummy2);

    [DllImport("avifil32.dll")]
    private static extern int AVIStreamRelease(IntPtr aviStream);

    [DllImport("avifil32.dll")]
    private static extern int AVIFileRelease(int pfile);

    [DllImport("avifil32.dll")]
    private static extern void AVIFileExit();

    private int pfile_ = 0;
    private IntPtr ps_ = new(0);
    private IntPtr psCompressed_ = new(0);
    private uint frameRate_ = 0;
    private int count_ = 0;
    private uint width_ = 0;
    private uint stride_ = 0;
    private uint height_ = 0;
    private readonly uint fccType_ = 1935960438;  // vids
    private readonly uint fccHandler_ = 808810089;// IV50
    //1145656899;  // CVID
    private Bitmap bmp_;
};
