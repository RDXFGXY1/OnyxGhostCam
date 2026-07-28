#include "OnyxMediaStream.h"
#include "OnyxGuids.h"
#include "OnyxLog.h"
#include "TestPattern.h"
#include <ksmedia.h>  // PINNAME_VIDEO_CAPTURE

using namespace Microsoft::WRL;

namespace onyx {

// Build the RGB32 media type shared by the stream descriptor and samples.
static HRESULT CreateOnyxMediaType(IMFMediaType** ppType)
{
    ComPtr<IMFMediaType> type;
    HRESULT hr = MFCreateMediaType(&type);
    if (FAILED(hr)) { return hr; }

    hr = type->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    if (FAILED(hr)) { return hr; }
    hr = type->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_NV12);
    if (FAILED(hr)) { return hr; }
    hr = type->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    if (FAILED(hr)) { return hr; }
    hr = type->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
    if (FAILED(hr)) { return hr; }
    hr = MFSetAttributeSize(type.Get(), MF_MT_FRAME_SIZE, kFrameWidth, kFrameHeight);
    if (FAILED(hr)) { return hr; }
    hr = MFSetAttributeRatio(type.Get(), MF_MT_FRAME_RATE, kFrameRateNum, kFrameRateDen);
    if (FAILED(hr)) { return hr; }
    hr = MFSetAttributeRatio(type.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
    if (FAILED(hr)) { return hr; }
    hr = type->SetUINT32(MF_MT_DEFAULT_STRIDE, kFrameStride);
    if (FAILED(hr)) { return hr; }
    hr = type->SetUINT32(MF_MT_SAMPLE_SIZE, kFrameSize);
    if (FAILED(hr)) { return hr; }

    *ppType = type.Detach();
    return S_OK;
}

OnyxMediaStream::OnyxMediaStream() = default;

HRESULT OnyxMediaStream::RuntimeClassInitialize(IMFMediaSource* parent, DWORD streamId)
{
    if (!parent) { return E_POINTER; }
    _parent = parent;
    _streamId = streamId;

    HRESULT hr = MFCreateEventQueue(&_eventQueue);
    if (FAILED(hr)) { return hr; }

    hr = CreateOnyxMediaType(&_mediaType);
    if (FAILED(hr)) { return hr; }

    // A stream descriptor advertising our single media type.
    IMFMediaType* types[] = { _mediaType.Get() };
    hr = MFCreateStreamDescriptor(_streamId, 1, types, &_streamDescriptor);
    if (FAILED(hr)) { return hr; }

    ComPtr<IMFMediaTypeHandler> handler;
    hr = _streamDescriptor->GetMediaTypeHandler(&handler);
    if (FAILED(hr)) { return hr; }
    hr = handler->SetCurrentMediaType(_mediaType.Get());
    if (FAILED(hr)) { return hr; }

    // These identify the stream to the Frame Server as a SHARED video-capture
    // stream. They must be set on BOTH the stream's own attribute store (returned
    // via GetStreamAttributes) AND the stream descriptor itself (which lives in
    // the presentation descriptor the Frame Server inspects). Setting them only on
    // a side store is why the Frame Server abandoned the source before Start.
    hr = MFCreateAttributes(&_streamAttributes, 4);
    if (FAILED(hr)) { return hr; }
    hr = SetStreamIdentity(_streamAttributes.Get());
    if (FAILED(hr)) { return hr; }
    hr = SetStreamIdentity(_streamDescriptor.Get());
    return hr;
}

HRESULT OnyxMediaStream::SetStreamIdentity(IMFAttributes* store)
{
    HRESULT hr = store->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE);
    if (FAILED(hr)) { return hr; }
    hr = store->SetUINT32(MF_DEVICESTREAM_STREAM_ID, _streamId);
    if (FAILED(hr)) { return hr; }
    hr = store->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1);
    if (FAILED(hr)) { return hr; }
    return store->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, MFFrameSourceTypes_Color);
}

HRESULT OnyxMediaStream::CopyStreamAttributes(IMFAttributes** ppAttributes)
{
    if (!ppAttributes) { return E_POINTER; }
    return _streamAttributes.CopyTo(ppAttributes);
}

HRESULT OnyxMediaStream::CheckShutdown() const
{
    return _shutdown ? MF_E_SHUTDOWN : S_OK;
}

// ---- IMFMediaEventGenerator ----

IFACEMETHODIMP OnyxMediaStream::GetEvent(DWORD flags, IMFMediaEvent** ppEvent)
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

IFACEMETHODIMP OnyxMediaStream::BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState)
{
    onyx::Log("Stream::BeginGetEvent");
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _eventQueue->BeginGetEvent(pCallback, punkState);
}

IFACEMETHODIMP OnyxMediaStream::EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent)
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _eventQueue->EndGetEvent(pResult, ppEvent);
}

IFACEMETHODIMP OnyxMediaStream::QueueEvent(MediaEventType met, REFGUID guidExtendedType,
                                           HRESULT hrStatus, const PROPVARIANT* pvValue)
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _eventQueue->QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
}

// ---- IMFMediaStream ----

IFACEMETHODIMP OnyxMediaStream::GetMediaSource(IMFMediaSource** ppMediaSource)
{
    if (!ppMediaSource) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _parent.CopyTo(ppMediaSource);
}

IFACEMETHODIMP OnyxMediaStream::GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor)
{
    if (!ppStreamDescriptor) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    return _streamDescriptor.CopyTo(ppStreamDescriptor);
}

IFACEMETHODIMP OnyxMediaStream::RequestSample(IUnknown* pToken)
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { onyx::Log("Stream::RequestSample shutdown"); return hr; }
    if (_state != MF_STREAM_STATE_RUNNING)
    {
        onyx::Log("Stream::RequestSample not-running state=%d", (int)_state);
        return MF_E_INVALIDREQUEST;
    }
    hr = DeliverSample(pToken);
    onyx::Log("Stream::RequestSample delivered frame=%llu hr=0x%08lX", _frameIndex, hr);
    return hr;
}

HRESULT OnyxMediaStream::DeliverSample(IUnknown* token)
{
    // Allocate a buffer and fill it with the current test-pattern frame.
    ComPtr<IMFMediaBuffer> buffer;
    HRESULT hr = MFCreateMemoryBuffer(kFrameSize, &buffer);
    if (FAILED(hr)) { return hr; }

    BYTE* data = nullptr;
    DWORD maxLen = 0;
    hr = buffer->Lock(&data, &maxLen, nullptr);
    if (FAILED(hr)) { return hr; }
    GenerateTestPatternNV12(data, kFrameWidth, kFrameHeight, _frameIndex++);
    buffer->Unlock();
    hr = buffer->SetCurrentLength(kFrameSize);
    if (FAILED(hr)) { return hr; }

    ComPtr<IMFSample> sample;
    hr = MFCreateSample(&sample);
    if (FAILED(hr)) { return hr; }
    hr = sample->AddBuffer(buffer.Get());
    if (FAILED(hr)) { return hr; }

    const LONGLONG duration = (10000000LL * kFrameRateDen) / kFrameRateNum;
    sample->SetSampleTime(_nextSampleTime);
    sample->SetSampleDuration(duration);
    _nextSampleTime += duration;

    if (token)
    {
        hr = sample->SetUnknown(MFSampleExtension_Token, token);
        if (FAILED(hr)) { return hr; }
    }

    return _eventQueue->QueueEventParamUnk(MEMediaSample, GUID_NULL, S_OK, sample.Get());
}

// ---- IMFMediaStream2 ----

IFACEMETHODIMP OnyxMediaStream::SetStreamState(MF_STREAM_STATE state)
{
    onyx::Log("Stream::SetStreamState state=%d", (int)state);
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    _state = state;
    return S_OK;
}

IFACEMETHODIMP OnyxMediaStream::GetStreamState(MF_STREAM_STATE* pState)
{
    if (!pState) { return E_POINTER; }
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    *pState = _state;
    return S_OK;
}

// ---- IMFAsyncCallback ----

IFACEMETHODIMP OnyxMediaStream::GetParameters(DWORD*, DWORD*)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP OnyxMediaStream::Invoke(IMFAsyncResult*)
{
    return S_OK;
}

// ---- IKsControl (minimal passthrough) ----

IFACEMETHODIMP OnyxMediaStream::KsProperty(PKSPROPERTY prop, ULONG propLen,
                                           LPVOID, ULONG, ULONG* br)
{
    if (br) { *br = 0; }
    if (prop && propLen >= sizeof(KSIDENTIFIER))
    {
        onyx::Log("Stream::KsProperty set={%08lX} id=%lu", prop->Set.Data1, prop->Id);
    }
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

IFACEMETHODIMP OnyxMediaStream::KsMethod(PKSMETHOD, ULONG, LPVOID, ULONG, ULONG* br)
{
    if (br) { *br = 0; }
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

IFACEMETHODIMP OnyxMediaStream::KsEvent(PKSEVENT, ULONG, LPVOID, ULONG, ULONG* br)
{
    if (br) { *br = 0; }
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

// ---- lifecycle ----

HRESULT OnyxMediaStream::Start()
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    _state = MF_STREAM_STATE_RUNNING;
    _nextSampleTime = 0;
    onyx::Log("Stream::Start -> RUNNING");
    return QueueEvent(MEStreamStarted, GUID_NULL, S_OK, nullptr);
}

HRESULT OnyxMediaStream::Stop()
{
    auto guard = _lock.LockExclusive();
    HRESULT hr = CheckShutdown();
    if (FAILED(hr)) { return hr; }
    _state = MF_STREAM_STATE_STOPPED;
    return QueueEvent(MEStreamStopped, GUID_NULL, S_OK, nullptr);
}

HRESULT OnyxMediaStream::Shutdown()
{
    auto guard = _lock.LockExclusive();
    _shutdown = true;
    if (_eventQueue)
    {
        _eventQueue->Shutdown();
        _eventQueue.Reset();
    }
    _streamDescriptor.Reset();
    _mediaType.Reset();
    _parent.Reset();
    return S_OK;
}

}  // namespace onyx
