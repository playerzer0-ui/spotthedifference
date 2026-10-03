using System;
using System.Runtime.InteropServices;

namespace spotthedifference
{
    internal static class ImageFilePicker
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int Size;
            public IntPtr Owner, Instance;
            public string Filter;
            public IntPtr CustomFilter;
            public int MaxCustomFilter, FilterIndex;
            public IntPtr File;
            public int MaxFile;
            public IntPtr FileTitle;
            public int MaxFileTitle;
            public string InitialDirectory, Title;
            public int Flags;
            public short FileOffset, ExtensionOffset;
            public string DefaultExtension;
            public IntPtr CustomData, Hook;
            public string Template;
            public IntPtr Reserved;
            public int ReservedValue, ExtendedFlags;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName options);

        [DllImport("comdlg32.dll")]
        private static extern int CommDlgExtendedError();

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public static string Choose(string title)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("The image picker currently supports Windows.");
            // OPENFILENAME contains a writable native buffer, not a StringBuilder field.
            // StringBuilder is supported as a P/Invoke parameter, but not inside this struct.
            IntPtr fileBuffer = Marshal.AllocHGlobal(32768 * sizeof(char));
            try
            {
                Marshal.WriteInt16(fileBuffer, 0);
                var options = new OpenFileName
                {
                    Size = Marshal.SizeOf<OpenFileName>(),
                    Owner = GetActiveWindow(),
                    Filter = "Images (PNG, JPG, BMP)\0*.png;*.jpg;*.jpeg;*.bmp\0\0",
                    FilterIndex = 1,
                    File = fileBuffer,
                    MaxFile = 32768,
                    Title = title,
                    Flags = 0x80000 | 0x1000 | 0x800 | 0x8 | 0x4
                };
                if (GetOpenFileName(ref options)) return Marshal.PtrToStringUni(fileBuffer);
                int error = CommDlgExtendedError();
                if (error != 0) throw new InvalidOperationException($"Image picker failed ({error}).");
                return null;
            }
            finally { Marshal.FreeHGlobal(fileBuffer); }
        }
    }
}
