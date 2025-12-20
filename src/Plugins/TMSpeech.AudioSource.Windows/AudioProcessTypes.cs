using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;

namespace TMSpeech.AudioSource.Windows;

// 重新定义需要的类型（基于 NAudio 的定义）

/// <summary>
/// 音频客户端激活参数
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AudioClientActivationParams
{
    public AudioClientActivationType ActivationType;
    public AudioClientProcessLoopbackParams ProcessLoopbackParams;
}

/// <summary>
/// 音频客户端激活类型
/// </summary>
internal enum AudioClientActivationType
{
    /// <summary>
    /// 默认激活
    /// </summary>
    Default,
    /// <summary>
    /// 进程环回激活
    /// </summary>
    ProcessLoopback
}

/// <summary>
/// 进程环回参数
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AudioClientProcessLoopbackParams
{
    /// <summary>
    /// 目标进程ID
    /// </summary>
    public uint TargetProcessId;
    /// <summary>
    /// 进程环回模式
    /// </summary>
    public ProcessLoopbackMode ProcessLoopbackMode;
}

/// <summary>
/// 进程环回模式
/// </summary>
internal enum ProcessLoopbackMode
{
    /// <summary>
    /// 包含目标进程树
    /// </summary>
    IncludeTargetProcessTree,
    /// <summary>
    /// 排除目标进程树
    /// </summary>
    ExcludeTargetProcessTree
}

/// <summary>
/// Blob 结构体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Blob
{
    public int Length;
    public IntPtr Data;
}

/// <summary>
/// PropVariant 结构体
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)]
    public short vt;
    [FieldOffset(8)]
    public Blob blobVal;
}

/// <summary>
/// 自定义 AgileObject 接口
/// </summary>
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90")]
internal interface ICustomAgileObject
{
}

/// <summary>
/// 进程音频激活完成处理器
/// </summary>
internal class ProcessActivationCompletionHandler : IActivateAudioInterfaceCompletionHandler, ICustomAgileObject
{
    private Action<IAudioClient> initializeAction;
    private TaskCompletionSource<IAudioClient> tcs = new TaskCompletionSource<IAudioClient>();

    public ProcessActivationCompletionHandler(
        Action<IAudioClient> initializeAction)
    {
        this.initializeAction = initializeAction;
    }

    public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
    {
        // First get the activation results, and see if anything bad happened then
        activateOperation.GetActivateResult(out int hr, out object unk);
        if (hr != 0)
        {
            tcs.TrySetException(Marshal.GetExceptionForHR(hr, new IntPtr(-1)));
            return;
        }

        var pAudioClient = (IAudioClient)unk;

        // Next try to call the client's (synchronous, blocking) initialization method.
        try
        {
            initializeAction(pAudioClient);
            tcs.SetResult(pAudioClient);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    public TaskAwaiter<IAudioClient> GetAwaiter()
    {
        return tcs.Task.GetAwaiter();
    }
}

/// <summary>
/// 原生方法定义
/// </summary>
internal static class NativeMethods
{
    [DllImport("mmdevapi.dll", CharSet = CharSet.Unicode)]
    public static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);
}