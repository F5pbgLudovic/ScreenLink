using System;
using System.Runtime.InteropServices;

namespace ScreenLinkPC
{
    // ---- Interfaces Media Foundation (déclarations complètes pour garder l'ordre des méthodes) ----
    [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFAttributes
    {
        [PreserveSig] int GetItem(ref Guid k, IntPtr v);
        [PreserveSig] int GetItemType(ref Guid k, out int t);
        [PreserveSig] int CompareItem(ref Guid k, IntPtr v, out bool r);
        [PreserveSig] int Compare(IMFAttributes o, int t, out bool r);
        [PreserveSig] int GetUINT32(ref Guid k, out uint v);
        [PreserveSig] int GetUINT64(ref Guid k, out ulong v);
        [PreserveSig] int GetDouble(ref Guid k, out double v);
        [PreserveSig] int GetGUID(ref Guid k, out Guid v);
        [PreserveSig] int GetStringLength(ref Guid k, out uint l);
        [PreserveSig] int GetString(ref Guid k, IntPtr b, uint c, out uint l);
        [PreserveSig] int GetAllocatedString(ref Guid k, out IntPtr b, out uint l);
        [PreserveSig] int GetBlobSize(ref Guid k, out uint s);
        [PreserveSig] int GetBlob(ref Guid k, IntPtr b, uint s, out uint o);
        [PreserveSig] int GetAllocatedBlob(ref Guid k, out IntPtr b, out uint s);
        [PreserveSig] int GetUnknown(ref Guid k, ref Guid riid, out IntPtr o);
        [PreserveSig] int SetItem(ref Guid k, IntPtr v);
        [PreserveSig] int DeleteItem(ref Guid k);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid k, uint v);
        [PreserveSig] int SetUINT64(ref Guid k, ulong v);
        [PreserveSig] int SetDouble(ref Guid k, double v);
        [PreserveSig] int SetGUID(ref Guid k, ref Guid v);
        [PreserveSig] int SetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] string v);
        [PreserveSig] int SetBlob(ref Guid k, IntPtr b, uint s);
        [PreserveSig] int SetUnknown(ref Guid k, IntPtr u);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint c);
        [PreserveSig] int GetItemByIndex(uint i, out Guid k, IntPtr v);
        [PreserveSig] int CopyAllItems(IMFAttributes d);
    }

    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFMediaType
    {
        [PreserveSig] int GetItem(ref Guid k, IntPtr v);
        [PreserveSig] int GetItemType(ref Guid k, out int t);
        [PreserveSig] int CompareItem(ref Guid k, IntPtr v, out bool r);
        [PreserveSig] int Compare(IMFAttributes o, int t, out bool r);
        [PreserveSig] int GetUINT32(ref Guid k, out uint v);
        [PreserveSig] int GetUINT64(ref Guid k, out ulong v);
        [PreserveSig] int GetDouble(ref Guid k, out double v);
        [PreserveSig] int GetGUID(ref Guid k, out Guid v);
        [PreserveSig] int GetStringLength(ref Guid k, out uint l);
        [PreserveSig] int GetString(ref Guid k, IntPtr b, uint c, out uint l);
        [PreserveSig] int GetAllocatedString(ref Guid k, out IntPtr b, out uint l);
        [PreserveSig] int GetBlobSize(ref Guid k, out uint s);
        [PreserveSig] int GetBlob(ref Guid k, IntPtr b, uint s, out uint o);
        [PreserveSig] int GetAllocatedBlob(ref Guid k, out IntPtr b, out uint s);
        [PreserveSig] int GetUnknown(ref Guid k, ref Guid riid, out IntPtr o);
        [PreserveSig] int SetItem(ref Guid k, IntPtr v);
        [PreserveSig] int DeleteItem(ref Guid k);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid k, uint v);
        [PreserveSig] int SetUINT64(ref Guid k, ulong v);
        [PreserveSig] int SetDouble(ref Guid k, double v);
        [PreserveSig] int SetGUID(ref Guid k, ref Guid v);
        [PreserveSig] int SetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] string v);
        [PreserveSig] int SetBlob(ref Guid k, IntPtr b, uint s);
        [PreserveSig] int SetUnknown(ref Guid k, IntPtr u);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint c);
        [PreserveSig] int GetItemByIndex(uint i, out Guid k, IntPtr v);
        [PreserveSig] int CopyAllItems(IMFAttributes d);
        [PreserveSig] int GetMajorType(out Guid g);
        [PreserveSig] int IsCompressedFormat(out bool b);
        [PreserveSig] int IsEqual(IMFMediaType t, out uint f);
        [PreserveSig] int GetRepresentation(Guid g, out IntPtr p);
        [PreserveSig] int FreeRepresentation(Guid g, IntPtr p);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFSample
    {
        [PreserveSig] int GetItem(ref Guid k, IntPtr v);
        [PreserveSig] int GetItemType(ref Guid k, out int t);
        [PreserveSig] int CompareItem(ref Guid k, IntPtr v, out bool r);
        [PreserveSig] int Compare(IMFAttributes o, int t, out bool r);
        [PreserveSig] int GetUINT32(ref Guid k, out uint v);
        [PreserveSig] int GetUINT64(ref Guid k, out ulong v);
        [PreserveSig] int GetDouble(ref Guid k, out double v);
        [PreserveSig] int GetGUID(ref Guid k, out Guid v);
        [PreserveSig] int GetStringLength(ref Guid k, out uint l);
        [PreserveSig] int GetString(ref Guid k, IntPtr b, uint c, out uint l);
        [PreserveSig] int GetAllocatedString(ref Guid k, out IntPtr b, out uint l);
        [PreserveSig] int GetBlobSize(ref Guid k, out uint s);
        [PreserveSig] int GetBlob(ref Guid k, IntPtr b, uint s, out uint o);
        [PreserveSig] int GetAllocatedBlob(ref Guid k, out IntPtr b, out uint s);
        [PreserveSig] int GetUnknown(ref Guid k, ref Guid riid, out IntPtr o);
        [PreserveSig] int SetItem(ref Guid k, IntPtr v);
        [PreserveSig] int DeleteItem(ref Guid k);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid k, uint v);
        [PreserveSig] int SetUINT64(ref Guid k, ulong v);
        [PreserveSig] int SetDouble(ref Guid k, double v);
        [PreserveSig] int SetGUID(ref Guid k, ref Guid v);
        [PreserveSig] int SetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] string v);
        [PreserveSig] int SetBlob(ref Guid k, IntPtr b, uint s);
        [PreserveSig] int SetUnknown(ref Guid k, IntPtr u);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint c);
        [PreserveSig] int GetItemByIndex(uint i, out Guid k, IntPtr v);
        [PreserveSig] int CopyAllItems(IMFAttributes d);
        [PreserveSig] int GetSampleFlags(out uint f);
        [PreserveSig] int SetSampleFlags(uint f);
        [PreserveSig] int GetSampleTime(out long t);
        [PreserveSig] int SetSampleTime(long t);
        [PreserveSig] int GetSampleDuration(out long t);
        [PreserveSig] int SetSampleDuration(long t);
        [PreserveSig] int GetBufferCount(out uint c);
        [PreserveSig] int GetBufferByIndex(uint i, out IMFMediaBuffer b);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer b);
        [PreserveSig] int AddBuffer(IMFMediaBuffer b);
        [PreserveSig] int RemoveBufferByIndex(uint i);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint l);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer b);
    }

    [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr p, out uint max, out uint cur);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint l);
        [PreserveSig] int SetCurrentLength(uint l);
        [PreserveSig] int GetMaxLength(out uint l);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MFT_OUTPUT_STREAM_INFO { public uint dwFlags; public uint cbSize; public uint cbAlignment; }

    [StructLayout(LayoutKind.Sequential)]
    struct MFT_OUTPUT_DATA_BUFFER { public uint dwStreamID; public IntPtr pSample; public uint dwStatus; public IntPtr pEvents; }

    [ComImport, Guid("bf94c121-5b05-4e6f-8000-ba598961414d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFTransform
    {
        [PreserveSig] int GetStreamLimits(out uint a, out uint b, out uint c, out uint d);
        [PreserveSig] int GetStreamCount(out uint i, out uint o);
        [PreserveSig] int GetStreamIDs(uint a, IntPtr b, uint c, IntPtr d);
        [PreserveSig] int GetInputStreamInfo(uint id, IntPtr info);
        [PreserveSig] int GetOutputStreamInfo(uint id, out MFT_OUTPUT_STREAM_INFO info);
        [PreserveSig] int GetAttributes(out IMFAttributes a);
        [PreserveSig] int GetInputStreamAttributes(uint id, out IMFAttributes a);
        [PreserveSig] int GetOutputStreamAttributes(uint id, out IMFAttributes a);
        [PreserveSig] int DeleteInputStream(uint id);
        [PreserveSig] int AddInputStreams(uint c, IntPtr ids);
        [PreserveSig] int GetInputAvailableType(uint id, uint idx, out IMFMediaType t);
        [PreserveSig] int GetOutputAvailableType(uint id, uint idx, out IMFMediaType t);
        [PreserveSig] int SetInputType(uint id, IMFMediaType t, uint flags);
        [PreserveSig] int SetOutputType(uint id, IMFMediaType t, uint flags);
        [PreserveSig] int GetInputCurrentType(uint id, out IMFMediaType t);
        [PreserveSig] int GetOutputCurrentType(uint id, out IMFMediaType t);
        [PreserveSig] int GetInputStatus(uint id, out uint f);
        [PreserveSig] int GetOutputStatus(out uint f);
        [PreserveSig] int SetOutputBounds(long lo, long hi);
        [PreserveSig] int ProcessEvent(uint id, IntPtr ev);
        [PreserveSig] int ProcessMessage(int msg, IntPtr param);
        [PreserveSig] int ProcessInput(uint id, IMFSample s, uint flags);
        [PreserveSig] int ProcessOutput(uint flags, uint count, IntPtr buffers, out uint status);
    }

    /// <summary>Décodeur H.264 matériel/logiciel de Windows (Media Foundation), sortie NV12.</summary>
    sealed class MfDecoder : IDisposable
    {
        [DllImport("mfplat.dll")] static extern int MFStartup(uint version, uint flags);
        [DllImport("mfplat.dll")] static extern int MFCreateMediaType(out IMFMediaType t);
        [DllImport("mfplat.dll")] static extern int MFCreateSample(out IMFSample s);
        [DllImport("mfplat.dll")] static extern int MFCreateMemoryBuffer(uint cb, out IMFMediaBuffer b);

        static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        static readonly Guid MF_MT_INTERLACE_MODE = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
        static readonly Guid MF_MT_DEFAULT_STRIDE = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        static readonly Guid MF_LOW_LATENCY = new Guid("9c27891a-ed7a-40e1-88e8-b22727a024ee");
        static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
        static readonly Guid MFVideoFormat_H264 = new Guid("34363248-0000-0010-8000-00AA00389B71");
        static readonly Guid MFVideoFormat_NV12 = new Guid("3231564E-0000-0010-8000-00AA00389B71");
        static readonly Guid CLSID_H264Decoder = new Guid("62CE7E72-4C71-4d20-B15D-452831A87D9D");

        const int MF_E_TRANSFORM_NEED_MORE_INPUT = unchecked((int)0xC00D6D72);
        const int MF_E_TRANSFORM_STREAM_CHANGE = unchecked((int)0xC00D6D61);
        const uint PROVIDES_SAMPLES = 0x100 | 0x200;   // PROVIDES_SAMPLES | CAN_PROVIDE_SAMPLES

        static bool started;

        IMFTransform mft;
        int outW, outH, outStride;
        bool providesSamples;
        uint outSize;
        long time;
        public string Error;

        /// <summary>Appelé pour chaque image décodée : pointeur NV12, pas (stride), largeur, hauteur.</summary>
        public Action<IntPtr, int, int, int> Frame;

        public MfDecoder(int w, int h)
        {
            if (!started) { int hr = MFStartup(0x00020070, 0); if (hr < 0) throw new Exception("MFStartup " + hr.ToString("X")); started = true; }
            mft = (IMFTransform)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_H264Decoder));

            IMFAttributes attr;
            if (mft.GetAttributes(out attr) == 0 && attr != null)
            {
                Guid g = MF_LOW_LATENCY;
                attr.SetUINT32(ref g, 1);
            }

            IMFMediaType t;
            Check(MFCreateMediaType(out t), "MFCreateMediaType");
            Guid k = MF_MT_MAJOR_TYPE, v = MFMediaType_Video;
            t.SetGUID(ref k, ref v);
            k = MF_MT_SUBTYPE; v = MFVideoFormat_H264;
            t.SetGUID(ref k, ref v);
            k = MF_MT_FRAME_SIZE;
            t.SetUINT64(ref k, ((ulong)(uint)w << 32) | (uint)h);
            k = MF_MT_INTERLACE_MODE;
            t.SetUINT32(ref k, 2);   // progressif
            Check(mft.SetInputType(0, t, 0), "SetInputType");
            SelectOutput();
            mft.ProcessMessage(0x10000000, IntPtr.Zero);   // NOTIFY_BEGIN_STREAMING
            mft.ProcessMessage(0x10000003, IntPtr.Zero);   // NOTIFY_START_OF_STREAM
        }

        static void Check(int hr, string what) { if (hr < 0) throw new Exception(what + " : 0x" + hr.ToString("X8")); }

        bool SelectOutput()
        {
            for (uint i = 0; i < 40; i++)
            {
                IMFMediaType t;
                int hr = mft.GetOutputAvailableType(0, i, out t);
                if (hr < 0) return false;
                Guid k = MF_MT_SUBTYPE, g;
                if (t.GetGUID(ref k, out g) == 0 && g == MFVideoFormat_NV12)
                {
                    if (mft.SetOutputType(0, t, 0) < 0) continue;
                    ulong fs; k = MF_MT_FRAME_SIZE;
                    if (t.GetUINT64(ref k, out fs) == 0) { outW = (int)(fs >> 32); outH = (int)(fs & 0xFFFFFFFF); }
                    uint st; k = MF_MT_DEFAULT_STRIDE;
                    outStride = (t.GetUINT32(ref k, out st) == 0 && (int)st > 0) ? (int)st : outW;
                    MFT_OUTPUT_STREAM_INFO info;
                    if (mft.GetOutputStreamInfo(0, out info) == 0)
                    {
                        providesSamples = (info.dwFlags & PROVIDES_SAMPLES) != 0;
                        outSize = info.cbSize;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Envoie un morceau de flux H.264 (format Annex B) et récupère les images prêtes.</summary>
        public void Decode(byte[] data, int off, int len)
        {
            IMFMediaBuffer buf;
            Check(MFCreateMemoryBuffer((uint)len, out buf), "MFCreateMemoryBuffer");
            IntPtr p; uint max, cur;
            buf.Lock(out p, out max, out cur);
            Marshal.Copy(data, off, p, len);
            buf.Unlock();
            buf.SetCurrentLength((uint)len);
            IMFSample s;
            Check(MFCreateSample(out s), "MFCreateSample");
            s.AddBuffer(buf);
            s.SetSampleTime(time);
            s.SetSampleDuration(333333);
            time += 333333;
            int hr = mft.ProcessInput(0, s, 0);
            Marshal.ReleaseComObject(buf);
            Marshal.ReleaseComObject(s);
            if (hr < 0 && hr != unchecked((int)0xC00D36B5)) { Error = "ProcessInput 0x" + hr.ToString("X8"); }
            Drain();
        }

        void Drain()
        {
            for (int guard = 0; guard < 8; guard++)
            {
                MFT_OUTPUT_DATA_BUFFER ob = new MFT_OUTPUT_DATA_BUFFER();
                IMFSample mine = null;
                IMFMediaBuffer mb = null;
                if (!providesSamples)
                {
                    uint size = outSize > 0 ? outSize : (uint)(outStride * outH * 3 / 2);
                    MFCreateSample(out mine);
                    MFCreateMemoryBuffer(size, out mb);
                    mine.AddBuffer(mb);
                    ob.pSample = Marshal.GetIUnknownForObject(mine);
                }
                IntPtr pob = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MFT_OUTPUT_DATA_BUFFER)));
                Marshal.StructureToPtr(ob, pob, false);
                uint status;
                int hr = mft.ProcessOutput(0, 1, pob, out status);
                ob = (MFT_OUTPUT_DATA_BUFFER)Marshal.PtrToStructure(pob, typeof(MFT_OUTPUT_DATA_BUFFER));
                Marshal.FreeHGlobal(pob);

                IMFSample outS = null;
                if (hr >= 0 && ob.pSample != IntPtr.Zero)
                {
                    outS = providesSamples ? (IMFSample)Marshal.GetObjectForIUnknown(ob.pSample) : mine;
                }
                if (ob.pSample != IntPtr.Zero) Marshal.Release(ob.pSample);
                if (ob.pEvents != IntPtr.Zero) Marshal.Release(ob.pEvents);

                if (hr >= 0 && outS != null)
                {
                    IMFMediaBuffer cb;
                    if (outS.ConvertToContiguousBuffer(out cb) == 0)
                    {
                        IntPtr q; uint max, cur;
                        if (cb.Lock(out q, out max, out cur) == 0)
                        {
                            if (Frame != null && outW > 0)
                            {
                                int stride = outStride;
                                // si le tampon est plus court que prévu, on déduit le pas
                                if (cur > 0 && stride * outH * 3 / 2 > cur) stride = (int)(cur * 2 / 3 / outH);
                                Frame(q, stride, outW, outH);
                            }
                            cb.Unlock();
                        }
                        Marshal.ReleaseComObject(cb);
                    }
                    if (providesSamples) Marshal.ReleaseComObject(outS);
                    if (mine != null) { Marshal.ReleaseComObject(mine); if (mb != null) Marshal.ReleaseComObject(mb); }
                    continue;
                }
                if (mine != null) { Marshal.ReleaseComObject(mine); if (mb != null) Marshal.ReleaseComObject(mb); }
                if (outS != null && providesSamples) Marshal.ReleaseComObject(outS);
                if (hr == MF_E_TRANSFORM_STREAM_CHANGE) { SelectOutput(); continue; }
                if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT) return;
                if (hr < 0) { Error = "ProcessOutput 0x" + hr.ToString("X8"); return; }
            }
        }

        public void Dispose()
        {
            if (mft != null)
            {
                try { mft.ProcessMessage(0, IntPtr.Zero); } catch { }
                try { Marshal.ReleaseComObject(mft); } catch { }
                mft = null;
            }
        }
    }
}
