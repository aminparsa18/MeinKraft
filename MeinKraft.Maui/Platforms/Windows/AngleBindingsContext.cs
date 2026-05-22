// MeinKraft.Maui/Platforms/Windows/AngleBindingsContext.cs
//
// Resolves OpenGL ES 3.0 function pointers from ANGLE (libEGL.dll + libGLESv2.dll).
// Pass an instance to GL.LoadBindings() on the first frame tick, once the
// SKGLView EGL context is guaranteed current.
//
// Resolution order:
//   1. eglGetProcAddress  — extension functions + most ES3 core functions
//   2. GetProcAddress(libGLESv2.dll) — core ES functions that are DLL exports
//      (some drivers return null from eglGetProcAddress for core entry points)

using OpenTK;
using System.Runtime.InteropServices;

public class AngleBindingsContext : IBindingsContext
{
    [DllImport("libEGL.dll")]
    private static extern IntPtr eglGetProcAddress(string procName);

    public IntPtr GetProcAddress(string procName) => eglGetProcAddress(procName);
}
