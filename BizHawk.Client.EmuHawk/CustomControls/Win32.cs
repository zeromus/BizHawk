using System;
using System.Security.Permissions;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;

namespace BizHawk.Client.EmuHawk
{
	public static class Win32
	{
		public static bool Is64BitProcess { get { return (IntPtr.Size == 8); } }
		public static bool Is64BitOperatingSystem { get { return Is64BitProcess || InternalCheckIsWow64(); } }

		[DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWow64Process(
				[In] IntPtr hProcess,
				[Out] out bool wow64Process
		);

		[DllImport("kernel32.dll")]
		public static extern IntPtr LoadLibrary(string dllToLoad);
		[DllImport("kernel32.dll")]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
		[DllImport("kernel32.dll")]
		public static extern bool FreeLibrary(IntPtr hModule);


		static bool InternalCheckIsWow64()
		{
			if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
					Environment.OSVersion.Version.Major >= 6)
			{
				using (var p = System.Diagnostics.Process.GetCurrentProcess())
				{
					bool retVal;
					if (!IsWow64Process(p.Handle, out retVal))
					{
						return false;
					}
					return retVal;
				}
			}
			else
			{
				return false;
			}
		}

		[StructLayout ( LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4 )]
		internal struct COMDLG_FILTERSPEC
{
    [MarshalAs ( UnmanagedType.LPWStr )]
    public string pszName;

    [MarshalAs ( UnmanagedType.LPWStr )]
    public string pszSpec;
}

		[ComImport, Guid ( "b4db1657-70d7-485e-8e3e-6fcb5a5c1802" ), InterfaceType ( ComInterfaceType.InterfaceIsIUnknown )]
internal interface IModalWindow
{
    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime ), PreserveSig]
    int Show ( [In] IntPtr parent );
}
		[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public interface IShellItem {
    void BindToHandler(IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)]Guid bhid,
        [MarshalAs(UnmanagedType.LPStruct)]Guid riid,
        out IntPtr ppv);

    void GetParent(out IShellItem ppsi);

    void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    void Compare(IShellItem psi, uint hint, out int piOrder);
};
		internal enum FDE_SHAREVIOLATION_RESPONSE
{
    FDESVR_DEFAULT = 0x00000000,
    FDESVR_ACCEPT = 0x00000001,
    FDESVR_REFUSE = 0x00000002
}public enum SIGDN : uint {
     NORMALDISPLAY = 0,
     PARENTRELATIVEPARSING = 0x80018001,
     PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
     DESKTOPABSOLUTEPARSING = 0x80028000,
     PARENTRELATIVEEDITING = 0x80031001,
     DESKTOPABSOLUTEEDITING = 0x8004c000,
     FILESYSPATH = 0x80058000,
     URL = 0x80068000
}
		[ComImport, Guid ( "973510DB-7D7F-452B-8975-74A85828D354" ), InterfaceType ( ComInterfaceType.InterfaceIsIUnknown )]
		internal interface IFileDialogEvents
{
    // NOTE: some of these callbacks are cancelable - returning S_FALSE means that
    // the dialog should not proceed (e.g. with closing, changing folder); to
    // support this, we need to use the PreserveSig attribute to enable us to return
    // the proper HRESULT
    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime ), PreserveSig]
    uint OnFileOk ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime ), PreserveSig]
    uint OnFolderChanging ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd,
                   [In, MarshalAs ( UnmanagedType.Interface )] IShellItem psiFolder );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void OnFolderChange ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void OnSelectionChange ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void OnShareViolation ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd,
                [In, MarshalAs ( UnmanagedType.Interface )] IShellItem psi,
                out FDE_SHAREVIOLATION_RESPONSE pResponse );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void OnTypeChange ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void OnOverwrite ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialog pfd,
               [In, MarshalAs ( UnmanagedType.Interface )] IShellItem psi,
               out FDE_OVERWRITE_RESPONSE pResponse );
}
		internal enum FDE_OVERWRITE_RESPONSE
{
    FDEOR_DEFAULT = 0x00000000,
    FDEOR_ACCEPT = 0x00000001,
    FDEOR_REFUSE = 0x00000002
}

		[Flags]
internal enum FOS : uint
{
    FOS_OVERWRITEPROMPT = 0x00000002,
    FOS_STRICTFILETYPES = 0x00000004,
    FOS_NOCHANGEDIR = 0x00000008,
    FOS_PICKFOLDERS = 0x00000020,
    FOS_FORCEFILESYSTEM = 0x00000040, // Ensure that items returned are filesystem items.
    FOS_ALLNONSTORAGEITEMS = 0x00000080, // Allow choosing items that have no storage.
    FOS_NOVALIDATE = 0x00000100,
    FOS_ALLOWMULTISELECT = 0x00000200,
    FOS_PATHMUSTEXIST = 0x00000800,
    FOS_FILEMUSTEXIST = 0x00001000,
    FOS_CREATEPROMPT = 0x00002000,
    FOS_SHAREAWARE = 0x00004000,
    FOS_NOREADONLYRETURN = 0x00008000,
    FOS_NOTESTFILECREATE = 0x00010000,
    FOS_HIDEMRUPLACES = 0x00020000,
    FOS_HIDEPINNEDPLACES = 0x00040000,
    FOS_NODEREFERENCELINKS = 0x00100000,
    FOS_DONTADDTORECENT = 0x02000000,
    FOS_FORCESHOWHIDDEN = 0x10000000,
    FOS_DEFAULTNOMINIMODE = 0x20000000
}
[ComImport, Guid ( "42f85136-db7e-439c-85f1-e4075d135fc8" ), InterfaceType ( ComInterfaceType.InterfaceIsIUnknown )]
internal interface IFileDialog : IModalWindow
{
    // Defined on IModalWindow - repeated here due to requirements of COM interop layer
    // --------------------------------------------------------------------------------
    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime ), PreserveSig]
    new int Show ( [In] IntPtr parent );

    // IFileDialog-Specific interface members
    // --------------------------------------------------------------------------------
    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetFileTypes ( [In] uint cFileTypes,
                [In, MarshalAs ( UnmanagedType.LPArray )] COMDLG_FILTERSPEC[] rgFilterSpec );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetFileTypeIndex ( [In] uint iFileType );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void GetFileTypeIndex ( out uint piFileType );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void Advise ( [In, MarshalAs ( UnmanagedType.Interface )] IFileDialogEvents pfde, out uint pdwCookie );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void Unadvise ( [In] uint dwCookie );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetOptions ( [In] FOS fos );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void GetOptions ( out FOS pfos );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetDefaultFolder ( [In, MarshalAs ( UnmanagedType.Interface )] IShellItem psi );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetFolder ( [In, MarshalAs ( UnmanagedType.Interface )] IShellItem psi );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void GetFolder ( [MarshalAs ( UnmanagedType.Interface )] out IShellItem ppsi );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void GetCurrentSelection ( [MarshalAs ( UnmanagedType.Interface )] out IShellItem ppsi );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetFileName ( [In, MarshalAs ( UnmanagedType.LPWStr )] string pszName );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void GetFileName ( [MarshalAs ( UnmanagedType.LPWStr )] out string pszName );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetTitle ( [In, MarshalAs ( UnmanagedType.LPWStr )] string pszTitle );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetOkButtonLabel ( [In, MarshalAs ( UnmanagedType.LPWStr )] string pszText );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetFileNameLabel ( [In, MarshalAs ( UnmanagedType.LPWStr )] string pszLabel );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void GetResult ( [MarshalAs ( UnmanagedType.Interface )] out IShellItem ppsi );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void AddPlace ( [In, MarshalAs ( UnmanagedType.Interface )] IShellItem psi, FDAP fdap );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetDefaultExtension ( [In, MarshalAs ( UnmanagedType.LPWStr )] string pszDefaultExtension );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void Close ( [MarshalAs ( UnmanagedType.Error )] int hr );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetClientGuid ( [In] ref Guid guid );

    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void ClearClientData ( );

    // Not supported:  IShellItemFilter is not defined, converting to IntPtr
    [MethodImpl ( MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime )]
    void SetFilter ( [MarshalAs ( UnmanagedType.Interface )] IntPtr pFilter );
    }

internal enum FDAP
{
	Bottom,
	Top
}

		[DllImport("shell32.dll")]
internal static extern uint SHSetTemporaryPropertyForItem(
  IShellItem     psi,
  ref PropertyKey propkey,
  ref PropVariant propvar
);

		    /// <summary>
    /// The structure to fix x64 and x32 variant size mismatch.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropArray
    {        
        uint _cElems;
        IntPtr _pElems;
    }

  /// <summary>
    /// COM VARIANT structure with special interface routines.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] private ushort _vt;

        /// <summary>
        /// IntPtr variant value.
        /// </summary>
        [FieldOffset(8)] private IntPtr _value;

        /*/// <summary>
        /// Byte variant value.
        /// </summary>
        [FieldOffset(8)] 
        private byte _ByteValue;*/

        /// <summary>
        /// Signed int variant value.
        /// </summary>
        [FieldOffset(8)]
        private Int32 _int32Value;

        /// <summary>
        /// Unsigned int variant value.
        /// </summary>
        [FieldOffset(8)] private UInt32 _uInt32Value;

        /// <summary>
        /// Long variant value.
        /// </summary>
        [FieldOffset(8)] private Int64 _int64Value;

        /// <summary>
        /// Unsigned long variant value.
        /// </summary>
        [FieldOffset(8)] private UInt64 _uInt64Value;

        /// <summary>
        /// FILETIME variant value.
        /// </summary>
        [FieldOffset(8)] private System.Runtime.InteropServices.ComTypes.FILETIME _fileTime;

        /// <summary>
        /// The PropArray instance to fix the variant size on x64 bit systems.
        /// </summary>
        [FieldOffset(8)]
        private PropArray _propArray;

        /// <summary>
        /// Gets or sets variant type.
        /// </summary>
        public VarEnum VarType
        {
            private get
            {
                return (VarEnum) _vt;
            }

            set
            {
                _vt = (ushort) value;
            }
        }

        /// <summary>
        /// Gets or sets the pointer value of the COM variant
        /// </summary>
        public IntPtr Value
        {
            private get
            {
                return _value;
            }

            set
            {
                _value = value;
            }
        }

        /*
        /// <summary>
        /// Gets or sets the byte value of the COM variant
        /// </summary>
        public byte ByteValue
        {
            get
            {
                return _ByteValue;
            }

            set
            {
                _ByteValue = value;
            }
        }
*/

        /// <summary>
        /// Gets or sets the UInt32 value of the COM variant.
        /// </summary>
        public UInt32 UInt32Value
        {
            private get
            {
                return _uInt32Value;
            }
            set
            {
                _uInt32Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the UInt32 value of the COM variant.
        /// </summary>
        public Int32 Int32Value
        {
            private get
            {
                return _int32Value;
            }
            set
            {
                _int32Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the Int64 value of the COM variant
        /// </summary>
        public Int64 Int64Value
        {
            private get
            {
                return _int64Value;
            }

            set
            {
                _int64Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the UInt64 value of the COM variant
        /// </summary>
        public UInt64 UInt64Value
        {
            private get
            {
                return _uInt64Value;
            }
            set
            {
                _uInt64Value = value;
            }
        }

        /*
        /// <summary>
        /// Gets or sets the FILETIME value of the COM variant
        /// </summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME FileTime
        {
            get
            {
                return _fileTime;
            }

            set
            {
                _fileTime = value;
            }
        }
*/

        /*/// <summary>
        /// Gets or sets variant type (ushort).
        /// </summary>
        public ushort VarTypeNative
        {
            get
            {
                return _vt;
            }

            set
            {
                _vt = value;
            }
        }*/

        /*/// <summary>
        /// Clears variant
        /// </summary>
        public void Clear()
        {
            switch (VarType)
            {
                case VarEnum.VT_EMPTY:
                    break;
                case VarEnum.VT_NULL:
                case VarEnum.VT_I2:
                case VarEnum.VT_I4:
                case VarEnum.VT_R4:
                case VarEnum.VT_R8:
                case VarEnum.VT_CY:
                case VarEnum.VT_DATE:
                case VarEnum.VT_ERROR:
                case VarEnum.VT_BOOL:
                case VarEnum.VT_I1:
                case VarEnum.VT_UI1:
                case VarEnum.VT_UI2:
                case VarEnum.VT_UI4:
                case VarEnum.VT_I8:
                case VarEnum.VT_UI8:
                case VarEnum.VT_INT:
                case VarEnum.VT_UINT:
                case VarEnum.VT_HRESULT:
                case VarEnum.VT_FILETIME:
                    _vt = 0;
                    break;
                default:
                    if (NativeMethods.PropVariantClear(ref this) != (int)OperationResult.Ok)
                    {
                        throw new ArgumentException("PropVariantClear has failed for some reason.");
                    }
                    break;
            }
        }*/

        /// <summary>
        /// Gets the object for this PropVariant.
        /// </summary>
        /// <returns></returns>
        public object Object
        {
            get
            {
#if !WINCE
                var sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Demand();
#endif
                switch (VarType)
                {
                    case VarEnum.VT_EMPTY:
                        return null;
                    case VarEnum.VT_FILETIME:
                        try
                        {
                            return DateTime.FromFileTime(Int64Value);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            return DateTime.MinValue;
                        }
                    default:
                        GCHandle propHandle = GCHandle.Alloc(this, GCHandleType.Pinned);
                        try
                        {
                            return Marshal.GetObjectForNativeVariant(propHandle.AddrOfPinnedObject());
                        }
#if WINCE
                        catch (NotSupportedException)
                        {
                            switch (VarType)
                            {
                                case VarEnum.VT_UI8:
                                    return UInt64Value;
                                case VarEnum.VT_UI4:
                                    return UInt32Value;
                                case VarEnum.VT_I8:
                                    return Int64Value;
                                case VarEnum.VT_I4:
                                    return Int32Value;
                                default:
                                    return 0;
                            }
                        }
#endif
                        finally
                        {
                            propHandle.Free();
                        }
                }
            }
        }

        /// <summary>
        /// Determines whether the specified System.Object is equal to the current PropVariant.
        /// </summary>
        /// <param name="obj">The System.Object to compare with the current PropVariant.</param>
        /// <returns>true if the specified System.Object is equal to the current PropVariant; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return (obj is PropVariant) ? Equals((PropVariant) obj) : false;
        }

        /// <summary>
        /// Determines whether the specified PropVariant is equal to the current PropVariant.
        /// </summary>
        /// <param name="afi">The PropVariant to compare with the current PropVariant.</param>
        /// <returns>true if the specified PropVariant is equal to the current PropVariant; otherwise, false.</returns>
        private bool Equals(PropVariant afi)
        {
            if (afi.VarType != VarType)
            {
                return false;
            }
            if (VarType != VarEnum.VT_BSTR)
            {
                return afi.Int64Value == Int64Value;
            }
            return afi.Value == Value;
        }

        /// <summary>
        ///  Serves as a hash function for a particular type.
        /// </summary>
        /// <returns> A hash code for the current PropVariant.</returns>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Returns a System.String that represents the current PropVariant.
        /// </summary>
        /// <returns>A System.String that represents the current PropVariant.</returns>
        public override string ToString()
        {
            return "[" + Value + "] " + Int64Value.ToString(CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Determines whether the specified PropVariant instances are considered equal.
        /// </summary>
        /// <param name="afi1">The first PropVariant to compare.</param>
        /// <param name="afi2">The second PropVariant to compare.</param>
        /// <returns>true if the specified PropVariant instances are considered equal; otherwise, false.</returns>
        public static bool operator ==(PropVariant afi1, PropVariant afi2)
        {
            return afi1.Equals(afi2);
        }

        /// <summary>
        /// Determines whether the specified PropVariant instances are not considered equal.
        /// </summary>
        /// <param name="afi1">The first PropVariant to compare.</param>
        /// <param name="afi2">The second PropVariant to compare.</param>
        /// <returns>true if the specified PropVariant instances are not considered equal; otherwise, false.</returns>
        public static bool operator !=(PropVariant afi1, PropVariant afi2)
        {
            return !afi1.Equals(afi2);
        }
    }

 /// <summary>
		/// PROPERTYKEY is defined in wtypes.h
		/// </summary>
		public struct PropertyKey
		{
			/// <summary>
			/// Format ID
			/// </summary>
			public Guid formatId;
			/// <summary>
			/// Property ID
			/// </summary>
			public int propertyId;

			// http://msdn.microsoft.com/en-us/library/windows/desktop/ff384862(v=vs.85).aspx
			// https://subversion.assembla.com/svn/portaudio/portaudio/trunk/src/hostapi/wasapi/mingw-include/propkey.h

			public PropertyKey(Guid guid, int propertyId)
			{
				this.formatId = guid;
				this.propertyId = propertyId;
			}
			public PropertyKey(string formatId, int propertyId)
				: this(new Guid(formatId), propertyId)
			{
			}
			public PropertyKey(uint a, uint b, uint c, uint d, uint e, uint f, uint g, uint h, uint i, uint j, uint k, int propertyId)
				: this(new Guid((uint)a, (ushort)b, (ushort)c, (byte)d, (byte)e, (byte)f, (byte)g, (byte)h, (byte)i, (byte)j, (byte)k), propertyId)
			{
			}
			public string GetBaseString()
			{
				return string.Format("{0},{1}", formatId.ToString(), propertyId.ToString());
			}

			//sample ("a45c254e-df1c-4efd-8020-67d146a850e0,2", "PKEY_Device_DeviceDesc")
			public static PropertyKey PKEY_ItemNameDisplay = new PropertyKey("B725F130-47EF-101A-A5F1-02608C9EEBAC", 4);
		}
	



		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct RECT
		{
			private int _Left;
			private int _Top;
			private int _Right;
			private int _Bottom;

			public RECT(RECT Rectangle)
				: this(Rectangle.Left, Rectangle.Top, Rectangle.Right, Rectangle.Bottom)
			{
			}
			public RECT(int Left, int Top, int Right, int Bottom)
			{
				_Left = Left;
				_Top = Top;
				_Right = Right;
				_Bottom = Bottom;
			}

			public int X
			{
				get { return _Left; }
				set { _Left = value; }
			}
			public int Y
			{
				get { return _Top; }
				set { _Top = value; }
			}
			public int Left
			{
				get { return _Left; }
				set { _Left = value; }
			}
			public int Top
			{
				get { return _Top; }
				set { _Top = value; }
			}
			public int Right
			{
				get { return _Right; }
				set { _Right = value; }
			}
			public int Bottom
			{
				get { return _Bottom; }
				set { _Bottom = value; }
			}
			public int Height
			{
				get { return _Bottom - _Top; }
				set { _Bottom = value - _Top; }
			}
			public int Width
			{
				get { return _Right - _Left; }
				set { _Right = value + _Left; }
			}
			public Point Location
			{
				get { return new Point(Left, Top); }
				set
				{
					_Left = value.X;
					_Top = value.Y;
				}
			}
			public Size Size
			{
				get { return new Size(Width, Height); }
				set
				{
					_Right = value.Width + _Left;
					_Bottom = value.Height + _Top;
				}
			}

			public static implicit operator Rectangle(RECT Rectangle)
			{
				return new Rectangle(Rectangle.Left, Rectangle.Top, Rectangle.Width, Rectangle.Height);
			}
			public static implicit operator RECT(Rectangle Rectangle)
			{
				return new RECT(Rectangle.Left, Rectangle.Top, Rectangle.Right, Rectangle.Bottom);
			}
			public static bool operator ==(RECT Rectangle1, RECT Rectangle2)
			{
				return Rectangle1.Equals(Rectangle2);
			}
			public static bool operator !=(RECT Rectangle1, RECT Rectangle2)
			{
				return !Rectangle1.Equals(Rectangle2);
			}

			public override string ToString()
			{
				return "{Left: " + _Left + "; " + "Top: " + _Top + "; Right: " + _Right + "; Bottom: " + _Bottom + "}";
			}

			public override int GetHashCode()
			{
				return ToString().GetHashCode();
			}

			public bool Equals(RECT Rectangle)
			{
				return Rectangle.Left == _Left && Rectangle.Top == _Top && Rectangle.Right == _Right && Rectangle.Bottom == _Bottom;
			}

			public override bool Equals(object Object)
			{
				if (Object is RECT)
				{
					return Equals((RECT)Object);
				}
				else if (Object is Rectangle)
				{
					return Equals(new RECT((Rectangle)Object));
				}

				return false;
			}
		}
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct AVISTREAMINFOW
		{
			public Int32 fccType;
			public Int32 fccHandler;
			public Int32 dwFlags;
			public Int32 dwCaps;
			public Int16 wPriority;
			public Int16 wLanguage;
			public Int32 dwScale;
			public Int32 dwRate;
			public Int32 dwStart;
			public Int32 dwLength;
			public Int32 dwInitialFrames;
			public Int32 dwSuggestedBufferSize;
			public Int32 dwQuality;
			public Int32 dwSampleSize;
			public RECT rcFrame;
			public Int32 dwEditCount;
			public Int32 dwFormatChangeCount;
			[MarshalAs(UnmanagedType.LPWStr, SizeConst=64)]
			public string szName;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct BITMAPINFOHEADER
		{
			public uint biSize;
			public int biWidth;
			public int biHeight;
			public ushort biPlanes;
			public ushort biBitCount;
			public uint biCompression;
			public uint biSizeImage;
			public int biXPelsPerMeter;
			public int biYPelsPerMeter;
			public uint biClrUsed;
			public uint biClrImportant;

			public void Init()
			{
				biSize = (uint)Marshal.SizeOf(this);
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct WAVEFORMATEX
		{
			public ushort wFormatTag;
			public ushort nChannels;
			public uint nSamplesPerSec;
			public uint nAvgBytesPerSec;
			public ushort nBlockAlign;
			public ushort wBitsPerSample;
			public ushort cbSize;

			public void Init()
			{
				cbSize = (ushort)Marshal.SizeOf(this);
			}
		}

		public const int WAVE_FORMAT_PCM = 1;
		public const int AVIIF_KEYFRAME = 0x00000010;


		[Flags]
		public enum OpenFileStyle : uint
		{
			OF_CANCEL = 0x00000800,  // Ignored. For a dialog box with a Cancel button, use OF_PROMPT.
			OF_CREATE = 0x00001000,  // Creates a new file. If file exists, it is truncated to zero (0) length.
			OF_DELETE = 0x00000200,  // Deletes a file.
			OF_EXIST = 0x00004000,  // Opens a file and then closes it. Used to test that a file exists
			OF_PARSE = 0x00000100,  // Fills the OFSTRUCT structure, but does not do anything else.
			OF_PROMPT = 0x00002000,  // Displays a dialog box if a requested file does not exist
			OF_READ = 0x00000000,  // Opens a file for reading only.
			OF_READWRITE = 0x00000002,  // Opens a file with read/write permissions.
			OF_REOPEN = 0x00008000,  // Opens a file by using information in the reopen buffer.

			// For MS-DOS–based file systems, opens a file with compatibility mode, allows any process on a
			// specified computer to open the file any number of times.
			// Other efforts to open a file with other sharing modes fail. This flag is mapped to the
			// FILE_SHARE_READ|FILE_SHARE_WRITE flags of the CreateFile function.
			OF_SHARE_COMPAT = 0x00000000,

			// Opens a file without denying read or write access to other processes.
			// On MS-DOS-based file systems, if the file has been opened in compatibility mode
			// by any other process, the function fails.
			// This flag is mapped to the FILE_SHARE_READ|FILE_SHARE_WRITE flags of the CreateFile function.
			OF_SHARE_DENY_NONE = 0x00000040,

			// Opens a file and denies read access to other processes.
			// On MS-DOS-based file systems, if the file has been opened in compatibility mode,
			// or for read access by any other process, the function fails.
			// This flag is mapped to the FILE_SHARE_WRITE flag of the CreateFile function.
			OF_SHARE_DENY_READ = 0x00000030,

			// Opens a file and denies write access to other processes.
			// On MS-DOS-based file systems, if a file has been opened in compatibility mode,
			// or for write access by any other process, the function fails.
			// This flag is mapped to the FILE_SHARE_READ flag of the CreateFile function.
			OF_SHARE_DENY_WRITE = 0x00000020,

			// Opens a file with exclusive mode, and denies both read/write access to other processes.
			// If a file has been opened in any other mode for read/write access, even by the current process,
			// the function fails.
			OF_SHARE_EXCLUSIVE = 0x00000010,

			// Verifies that the date and time of a file are the same as when it was opened previously.
			// This is useful as an extra check for read-only files.
			OF_VERIFY = 0x00000400,

			// Opens a file for write access only.
			OF_WRITE = 0x00000001
		}

		[DllImport("avifil32.dll", SetLastError = true)]
		public static extern int AVIFileOpenW(ref IntPtr pAviFile, [MarshalAs(UnmanagedType.LPWStr)] string szFile, OpenFileStyle uMode, int lpHandler);

		[DllImport("avifil32.dll", SetLastError = true)]
		public static extern void AVIFileInit();

		// Create a new stream in an existing file and creates an interface to the new stream
		[DllImport("avifil32.dll")]
		public static extern int AVIFileCreateStreamW(
			IntPtr pfile,
			out IntPtr ppavi,
			ref AVISTREAMINFOW psi);

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct AVICOMPRESSOPTIONS
		{
			public int fccType;
			public int fccHandler;
			public int dwKeyFrameEvery;
			public int dwQuality;
			public int dwBytesPerSecond;
			public int dwFlags;
			public int lpFormat;
			public int cbFormat;
			public int lpParms;
			public int cbParms;
			public int dwInterleaveEvery;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		public static extern SafeFileHandle CreateFile(
			  string lpFileName,
			  uint dwDesiredAccess,
			  uint dwShareMode,
			  IntPtr SecurityAttributes,
			  uint dwCreationDisposition,
			  uint dwFlagsAndAttributes,
			  IntPtr hTemplateFile
			  );

		[DllImport("kernel32.dll")]
		public static extern FileType GetFileType(IntPtr hFile);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetCommandLine();

		public enum FileType : uint
		{
			FileTypeChar = 0x0002,
			FileTypeDisk = 0x0001,
			FileTypePipe = 0x0003,
			FileTypeRemote = 0x8000,
			FileTypeUnknown = 0x0000,
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr SetActiveWindow(IntPtr hWnd);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool AttachConsole(int dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool AllocConsole();

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern bool FreeConsole();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetStdHandle(int nStdHandle, IntPtr hConsoleOutput);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern IntPtr CreateFile(
			string fileName,
			int desiredAccess,
			int shareMode,
			IntPtr securityAttributes,
			int creationDisposition,
			int flagsAndAttributes,
			IntPtr templateFile);

		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
		public static extern bool CloseHandle(IntPtr handle); 

		[DllImport("user32.dll", SetLastError = false)]
		public static extern IntPtr GetDesktopWindow();

		// Retrieve the save options for a file and returns them in a buffer 
		[DllImport("avifil32.dll")]
		public static extern int AVISaveOptions(
			IntPtr hwnd,
			int flags,
			int streams,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] IntPtr[] ppavi,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] IntPtr[] plpOptions);

		// Free the resources allocated by the AVISaveOptions function 
		[DllImport("avifil32.dll")]
		public static extern int AVISaveOptionsFree(
			int streams,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] plpOptions);

		// Create a compressed stream from an uncompressed stream and a
		// compression filter, and returns the address of a pointer to
		// the compressed stream
		[DllImport("avifil32.dll")]
		public static extern int AVIMakeCompressedStream(
			out IntPtr ppsCompressed,
			IntPtr psSource,
			ref AVICOMPRESSOPTIONS lpOptions,
			IntPtr pclsidHandler);

		// Set the format of a stream at the specified position
		[DllImport("avifil32.dll")]
		public static extern int AVIStreamSetFormat(
			IntPtr pavi,
			int lPos,
			ref BITMAPINFOHEADER lpFormat,
			int cbFormat);

		// Set the format of a stream at the specified position
		[DllImport("avifil32.dll")]
		public static extern int AVIStreamSetFormat(
			IntPtr pavi,
			int lPos,
			ref WAVEFORMATEX lpFormat,
			int cbFormat);

		// Write data to a stream
		[DllImport("avifil32.dll")]
		public static extern int AVIStreamWrite(
			IntPtr pavi,
			int lStart,
			int lSamples,
			IntPtr lpBuffer,
			int cbBuffer,
			int dwFlags,
			IntPtr plSampWritten,
			out int plBytesWritten);

		// Release an open AVI stream
		[DllImport("avifil32.dll")]
		public static extern int AVIStreamRelease(
			IntPtr pavi);

		// Release an open AVI stream
		[DllImport("avifil32.dll")]
		public static extern int AVIFileRelease(
			IntPtr pfile);


		// Replacement of mmioFOURCC macros
		public static int mmioFOURCC(string str)
		{
			return (
				((int)(byte)(str[0])) |
				((int)(byte)(str[1]) << 8) |
				((int)(byte)(str[2]) << 16) |
				((int)(byte)(str[3]) << 24));
		}

		public static bool FAILED(int hr) { return hr < 0; }



		// Inverse of mmioFOURCC
		public static string decode_mmioFOURCC(int code)
		{
			char[] chs = new char[4];

			for (int i = 0; i < 4; i++)
			{
				chs[i] = (char)(byte)((code >> (i << 3)) & 0xFF);
				if (!char.IsLetterOrDigit(chs[i]))
					chs[i] = ' ';
			}
			return new string(chs);
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

		[DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
		public static extern void ZeroMemory(IntPtr dest, uint size);

		[DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
		public static extern IntPtr MemSet(IntPtr dest, int c, uint count);

		[DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
		public static extern bool PathRelativePathTo(
			 [Out] System.Text.StringBuilder pszPath,
			 [In] string pszFrom,
			 [In] FileAttributes dwAttrFrom,
			 [In] string pszTo,
			 [In] FileAttributes dwAttrTo
		);

		/// <summary>
		/// File attributes are metadata values stored by the file system on disk and are used by the system and are available to developers via various file I/O APIs.
		/// </summary>
		[Flags]
		//[CLSCompliant(false)]
		public enum FileAttributes : uint
		{
			/// <summary>
			/// A file that is read-only. Applications can read the file, but cannot write to it or delete it. This attribute is not honored on directories. For more information, see "You cannot view or change the Read-only or the System attributes of folders in Windows Server 2003, in Windows XP, or in Windows Vista".
			/// </summary>
			Readonly = 0x00000001,

			/// <summary>
			/// The file or directory is hidden. It is not included in an ordinary directory listing.
			/// </summary>
			Hidden = 0x00000002,

			/// <summary>
			/// A file or directory that the operating system uses a part of, or uses exclusively.
			/// </summary>
			System = 0x00000004,

			/// <summary>
			/// The handle that identifies a directory.
			/// </summary>
			Directory = 0x00000010,

			/// <summary>
			/// A file or directory that is an archive file or directory. Applications typically use this attribute to mark files for backup or removal.
			/// </summary>
			Archive = 0x00000020,

			/// <summary>
			/// This value is reserved for system use.
			/// </summary>
			Device = 0x00000040,

			/// <summary>
			/// A file that does not have other attributes set. This attribute is valid only when used alone.
			/// </summary>
			Normal = 0x00000080,

			/// <summary>
			/// A file that is being used for temporary storage. File systems avoid writing data back to mass storage if sufficient cache memory is available, because typically, an application deletes a temporary file after the handle is closed. In that scenario, the system can entirely avoid writing the data. Otherwise, the data is written after the handle is closed.
			/// </summary>
			Temporary = 0x00000100,

			/// <summary>
			/// A file that is a sparse file.
			/// </summary>
			SparseFile = 0x00000200,

			/// <summary>
			/// A file or directory that has an associated reparse point, or a file that is a symbolic link.
			/// </summary>
			ReparsePoint = 0x00000400,

			/// <summary>
			/// A file or directory that is compressed. For a file, all of the data in the file is compressed. For a directory, compression is the default for newly created files and subdirectories.
			/// </summary>
			Compressed = 0x00000800,

			/// <summary>
			/// The data of a file is not available immediately. This attribute indicates that the file data is physically moved to offline storage. This attribute is used by Remote Storage, which is the hierarchical storage management software. Applications should not arbitrarily change this attribute.
			/// </summary>
			Offline = 0x00001000,

			/// <summary>
			/// The file or directory is not to be indexed by the content indexing service.
			/// </summary>
			NotContentIndexed = 0x00002000,

			/// <summary>
			/// A file or directory that is encrypted. For a file, all data streams in the file are encrypted. For a directory, encryption is the default for newly created files and subdirectories.
			/// </summary>
			Encrypted = 0x00004000,

			/// <summary>
			/// This value is reserved for system use.
			/// </summary>
			Virtual = 0x00010000
		}
	}



}