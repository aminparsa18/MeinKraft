using OpenTK;
using System.Runtime.InteropServices;

public class AndroidBindingsContext : IBindingsContext
{
    private readonly IntPtr _libHandle;

    public AndroidBindingsContext()
    {
        _libHandle = NativeLibrary.Load("libGLESv2.so");
        if (_libHandle == IntPtr.Zero)
        {
            _libHandle = NativeLibrary.Load("libGLESv2.so"); // some drivers expose ES3 via v2 lib
        }
    }

    public IntPtr GetProcAddress(string procName)
    {
        if (NativeLibrary.TryGetExport(_libHandle, procName, out IntPtr ptr))
            return ptr;

        return IntPtr.Zero;
    }
}