using System;
using FavoriteHelper;

internal static class CompatibilityTests
{
    private static int passed;

    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        Console.WriteLine("PASS " + name);
        passed++;
    }

    public static int Main()
    {
        Check(WindowClassifier.IsPhotosProcessName("PhotosApp"), "Windows 10 PhotosApp identity");
        Check(WindowClassifier.IsPhotosProcessName("photosapp"), "Windows 10 identity is case-insensitive");
        Check(WindowClassifier.IsPhotosProcessName("Photos"), "Windows 11 Photos identity");
        Check(WindowClassifier.IsPhotosProcessName("PHOTOS"), "Windows 11 identity is case-insensitive");
        Check(!WindowClassifier.IsPhotosProcessName(null), "null identity fails closed");
        Check(!WindowClassifier.IsPhotosProcessName(String.Empty), "empty identity fails closed");
        Check(!WindowClassifier.IsPhotosProcessName("Microsoft.Photos"), "unverified identity is rejected");
        Check(!WindowClassifier.IsPhotosProcessName("MyPhotoEditor"), "broad photo-name match is rejected");
        Check(WindowClassifier.Classify(IntPtr.Zero) == ForegroundKind.Other, "zero HWND fails closed");
        Check(WindowClassifier.PhotosPid(IntPtr.Zero) == 0, "zero HWND has no Photos PID");
        Console.WriteLine("COMPATIBILITY ALL PASS (" + passed + ")");
        return 0;
    }
}
