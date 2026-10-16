#if WINDOWS
using System.Runtime.InteropServices;

namespace AppleMusicControl.Platform;

// Declarations follow mmdeviceapi.h and endpointvolume.h. Vtable order matters, so methods before the
// ones used keep their slots.
internal static class CoreAudioInterop
{
	public const int ClsCtxAll = 0x17;

	public enum EDataFlow
	{
		Render = 0
	}

	public enum ERole
	{
		Multimedia = 1
	}

	[ComImport]
	[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	public class MMDeviceEnumerator
	{
	}

	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMMDeviceEnumerator
	{
		[PreserveSig]
		int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);

		[PreserveSig]
		int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMMDevice
	{
		[PreserveSig]
		int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
	}

	[ComImport]
	[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAudioEndpointVolume
	{
		[PreserveSig]
		int RegisterControlChangeNotify(IntPtr notify);

		[PreserveSig]
		int UnregisterControlChangeNotify(IntPtr notify);

		[PreserveSig]
		int GetChannelCount(out uint channelCount);

		[PreserveSig]
		int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

		[PreserveSig]
		int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

		[PreserveSig]
		int GetMasterVolumeLevel(out float levelDb);

		[PreserveSig]
		int GetMasterVolumeLevelScalar(out float level);
	}
}
#endif
