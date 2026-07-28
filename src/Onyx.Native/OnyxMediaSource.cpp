#include "OnyxMediaSource.h"
#include "OnyxGuids.h"
#include "OnyxLog.h"
#include <ksmedia.h>  // PROPSETID_VIDCAP_CAMERACONTROL, KSPROPERTY_CAMERACONTROL_*

using namespace Microsoft::WRL;

namespace onyx {

OnyxMediaSource::OnyxMediaSource() = default;

HRESULT OnyxMediaSource::RuntimeClassInitialize()
{
    HRESULT hr = InitAgile();
    if (FAILED(hr)) { return hr; }

    hr = MFCreateEventQueue(&_eventQueue);
    if (FAILED(hr)) { return hr; }

    hr = MFCreateAttributes(&_attributes, 1);
    if (FAILED(hr)) { return hr; }
    // Identify this as a video-capture device source.
    hr = _attributes->SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
                              MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
    if (FAILED(hr)) { return hr; }

    // The single video stream (id 0).
    hr = MakeAndInitialize<OnyxMediaStream>(&_stream, this, /*streamId*/ 0);
    if (FAILED(hr)) { return hr; }

    hr = _stream->GetStreamDescriptor(&_streamDescriptor);
    return hr;
}

HRESULT OnyxMediaSource::CheckShutdown() const
{
    return _shutdown ? MF_E_SHUTDOWN : S_OK;
}

HRESULT OnyxMediaSource::CreatePresentationDescriptorLocked(IMFPresentationDescriptor** ppPD)
{
    IMFStreamDescriptor* sds[] = { _streamDescriptor.Get() };
    ComPtr<IMFPresentationDescriptor> pd;
    HRESULT hr = MFCreatePresentationDescriptor(1, sds, &pd);
    if (FAILED(hr)) { return hr; }

    hr = pd->SelectStream(0);
    if (FAILED(hr)) { return hr; }

    *ppPD = pd.Detach();
    return S_OK;
}

// ---- IMFMediaEventGenerator ----

IFACEMETHODIMP OnyxMediaSource::GetEvent(DWORD flags, IMFMediaEvent** ppEvent)
{
    ComPtr<IMFMediaEventQueue> queue;
    {
        auto guard = _lock.LockExclusive();
        HRESULT hr = CheckShutdown();
        if (FAILED(hr)) { return hr; }
        queue = _eventQueue;
    }
    return queue->GetEvent(flags, ppEvent);
}

IFACEMETHODIMP OnyxMediaSource::BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState)
{
    onyx::Log("Source::BeginGetEvent");
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _eventQueue->BeginGetEvent(pCallback, punkState);
}

IFACEMETHODIMP OnyxMediaSource::EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent)
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _eventQueue->EndGetEvent(pResult, ppEvent);
}

IFACEMETHODIMP OnyxMediaSource::QueueEvent(MediaEventType met, REFGUID guidExtendedType,
                                           HRESULT hrStatus, const PROPVARIANT* pvValue)
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _eventQueue->QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
}

// ---- IMFMediaSource ----

IFACEMETHODIMP OnyxMediaSource::GetCharacteristics(DWORD* pdwCharacteristics)
{
    if (!pdwCharacteristics) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    *pdwCharacteristics = MFMEDIASOURCE_IS_LIVE;
    onyx::Log("Source::GetCharacteristics");
    return S_OK;
}

IFACEMETHODIMP OnyxMediaSource::CreatePresentationDescriptor(IMFPresentationDescriptor** ppPD)
{
    if (!ppPD) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    hr = CreatePresentationDescriptorLocked(ppPD);
    onyx::Log("Source::CreatePresentationDescriptor hr=0x%08lX", hr);
    return hr;
}

IFACEMETHODIMP OnyxMediaSource::Start(IMFPresentationDescriptor* pPD, const GUID*,
                                      const PROPVARIANT* pvarStartPos)
{
    onyx::Log("Source::Start enter");
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { onyx::Log("Source::Start shutdown 0x%08lX", hr); return hr; }
    if (!pPD) { return E_INVALIDARG; }

    // Announce the stream: MENewStream the first time, MEUpdatedStream after.
    const MediaEventType streamEvent = _started ? MEUpdatedStream : MENewStream;
    hr = _eventQueue->QueueEventParamUnk(streamEvent, GUID_NULL, S_OK,
                                         static_cast<IMFMediaStream*>(_stream.Get()));
    if (FAILED(hr)) { return hr; }

    hr = _stream->Start();
    if (FAILED(hr)) { return hr; }

    _started = true;

    PROPVARIANT startTime;
    PropVariantInit(&startTime);
    if (pvarStartPos) { startTime = *pvarStartPos; }
    hr = _eventQueue->QueueEventParamVar(MESourceStarted, GUID_NULL, S_OK, &startTime);
    PropVariantClear(&startTime);
    onyx::Log("Source::Start exit 0x%08lX", hr);
    return hr;
}

IFACEMETHODIMP OnyxMediaSource::Stop()
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }

    hr = _stream->Stop();
    if (FAILED(hr)) { return hr; }

    return _eventQueue->QueueEventParamVar(MESourceStopped, GUID_NULL, S_OK, nullptr);
}

IFACEMETHODIMP OnyxMediaSource::Pause()
{
    // Live source: pause is not supported.
    return MF_E_INVALID_STATE_TRANSITION;
}

IFACEMETHODIMP OnyxMediaSource::Shutdown()
{
    auto guard = _lock.LockExclusive();
    _shutdown = true;
    if (_stream) { _stream->Shutdown(); }
    if (_eventQueue)
    {
        _eventQueue->Shutdown();
        _eventQueue.Reset();
    }
    _stream.Reset();
    _streamDescriptor.Reset();
    _attributes.Reset();
    return S_OK;
}

// ---- IMFMediaSourceEx ----

IFACEMETHODIMP OnyxMediaSource::GetSourceAttributes(IMFAttributes** ppAttributes)
{
    if (!ppAttributes) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    onyx::Log("Source::GetSourceAttributes");
    return _attributes.CopyTo(ppAttributes);
}

IFACEMETHODIMP OnyxMediaSource::GetStreamAttributes(DWORD, IMFAttributes** ppAttributes)
{
    if (!ppAttributes) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    if (!_stream) { return MF_E_INVALIDSTREAMNUMBER; }
    onyx::Log("Source::GetStreamAttributes");
    return _stream->CopyStreamAttributes(ppAttributes);
}

IFACEMETHODIMP OnyxMediaSource::SetD3DManager(IUnknown* pManager)
{
    // Software source: no D3D acceleration for now.
    onyx::Log("Source::SetD3DManager mgr=%p", (void*)pManager);
    return S_OK;
}

// ---- IMFGetService ----

IFACEMETHODIMP OnyxMediaSource::GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject)
{
    if (!ppvObject) { return E_POINTER; }
    HRESULT hr = QueryInterface(riid, ppvObject);
    onyx::Log("Source::GetService svc={%08lX} hr=0x%08lX", guidService.Data1, hr);
    return hr;
}

// ---- IKsControl ----

IFACEMETHODIMP OnyxMediaSource::KsProperty(PKSPROPERTY prop, ULONG propLen,
                                           LPVOID data, ULONG dataLen, ULONG* bytesReturned)
{
    if (bytesReturned) { *bytesReturned = 0; }
    if (!prop || propLen < sizeof(KSPROPERTY)) { return E_INVALIDARG; }

    onyx::Log("Source::KsProperty set={%08lX} id=%lu flags=%lu",
              prop->Set.Data1, prop->Id, prop->Flags);

    // The Frame Server queries the camera PRIVACY control before it will stream
    // (Windows 11 privacy enforcement). We must report privacy is OFF, otherwise
    // it fails safe and refuses to start the stream.
    if (IsEqualGUID(prop->Set, PROPSETID_VIDCAP_CAMERACONTROL) &&
        prop->Id == KSPROPERTY_CAMERACONTROL_PRIVACY)
    {
        if (prop->Flags & KSPROPERTY_TYPE_BASICSUPPORT)
        {
            if (dataLen < sizeof(ULONG))
            {
                if (bytesReturned) { *bytesReturned = sizeof(ULONG); }
                return HRESULT_FROM_WIN32(ERROR_MORE_DATA);
            }
            *reinterpret_cast<ULONG*>(data) = KSPROPERTY_TYPE_GET | KSPROPERTY_TYPE_BASICSUPPORT;
            if (bytesReturned) { *bytesReturned = sizeof(ULONG); }
            return S_OK;
        }
        if (prop->Flags & KSPROPERTY_TYPE_GET)
        {
            if (dataLen < sizeof(KSPROPERTY_CAMERACONTROL_S))
            {
                if (bytesReturned) { *bytesReturned = sizeof(KSPROPERTY_CAMERACONTROL_S); }
                return HRESULT_FROM_WIN32(ERROR_MORE_DATA);
            }
            auto* s = reinterpret_cast<KSPROPERTY_CAMERACONTROL_S*>(data);
            ZeroMemory(s, sizeof(*s));
            s->Value = 0;  // 0 = privacy OFF (streaming allowed)
            s->Flags = KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;
            s->Capabilities = KSPROPERTY_CAMERACONTROL_FLAGS_MANUAL;
            if (bytesReturned) { *bytesReturned = sizeof(KSPROPERTY_CAMERACONTROL_S); }
            return S_OK;
        }
    }

    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

IFACEMETHODIMP OnyxMediaSource::KsMethod(PKSMETHOD, ULONG, LPVOID, ULONG, ULONG* br)
{
    if (br) { *br = 0; }
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

IFACEMETHODIMP OnyxMediaSource::KsEvent(PKSEVENT, ULONG, LPVOID, ULONG, ULONG* br)
{
    if (br) { *br = 0; }
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

// ---- IMFSampleAllocatorControl ----

IFACEMETHODIMP OnyxMediaSource::SetDefaultAllocator(DWORD, IUnknown*)
{
    // We produce our own memory buffers, so we accept but ignore any allocator.
    return S_OK;
}

IFACEMETHODIMP OnyxMediaSource::GetAllocatorUsage(DWORD dwOutputStreamID, DWORD* pdwInputStreamID,
                                                  MFSampleAllocatorUsage* peUsage)
{
    if (!pdwInputStreamID || !peUsage) { return E_POINTER; }
    *pdwInputStreamID = dwOutputStreamID;
    // Milestone 1: the stream allocates its own samples (see DeliverSample).
    *peUsage = MFSampleAllocatorUsage_DoesNotAllocate;
    onyx::Log("Source::GetAllocatorUsage -> DoesNotAllocate");
    return S_OK;
}

}  // namespace onyx
