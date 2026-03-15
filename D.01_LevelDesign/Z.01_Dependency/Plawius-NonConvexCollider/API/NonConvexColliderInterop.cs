using System;
using System.Runtime.InteropServices;

namespace Plawius.NonConvexCollider
{
    // V-HACD Interop (original)
    internal static class Interop
    {
        [DllImport("libvhacd")]
        internal static extern int GetMeshEx(IntPtr points,
                                             int poitns_size,
                                             IntPtr triangles,
                                             int triangles_size,
                                             out IntPtr out_points,
                                             out IntPtr out_triangles,
                                             out IntPtr indexes,
                                             out int indexes_cnt,
                                             Parameters prms);

        [DllImport("libvhacd")]
        internal static extern int ReleaseMemory(IntPtr ptr);
    }

    // CoACD Interop (New v2)
    internal static class InteropCoACD
    {
        [DllImport("libcoacd")]
        internal static extern int GetMeshExFloat(IntPtr points,
                                                   int points_size,
                                                   IntPtr triangles,
                                                   int triangles_size,
                                                   out IntPtr out_points,
                                                   out IntPtr out_triangles,
                                                   out IntPtr indexes,
                                                   out int indexes_cnt,
                                                   CoACDParameters prms);

        [DllImport("libcoacd")]
        internal static extern void ReleaseMemory(IntPtr ptr);
    }
}