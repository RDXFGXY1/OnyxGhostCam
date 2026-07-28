#pragma once

#include <windows.h>
#include <mfidl.h>
#include <mfapi.h>
#include <mferror.h>
#include <wrl.h>
#include <ks.h>
#include <ksproxy.h>
#include <cstdint>
#include "OnyxAgile.h"

// Onyx media stream (milestone 1b).
//
// One video stream of the Onyx software media source. Serves RGB32 frames
// produced by the test-pattern generator, delivered asynchronously in response
// to IMFMediaStream::RequestSample.

namespace onyx {

class OnyxMediaStream
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          Microsoft::WRL::ChainInterfaces<IMFMediaStream2, IMFMediaStream, IMFMediaEventGenerator>,
          IMFAsyncCallback,
          IKsControl,
          Microsoft::WRL::CloakedIid<IMarshal>>
{
public:
    OnyxMediaStream();

    // Two-phase init so we can return HRESULTs from setup.
    HRESULT RuntimeClassInitialize(IMFMediaSource* parent, DWORD streamId);

    // ---- IMFMediaEventGenerator ----
    IFACEMETHODIMP GetEvent(DWORD flags, IMFMediaEvent** ppEvent) override;
    IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState) override;
    IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent) override;
    IFACEMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType,
                              HRESULT hrStatus, const PROPVARIANT* pvValue) override;

    // ---- IMFMediaStream ----
    IFACEMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource) override;
    IFACEMETHODIMP GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor) override;
    IFACEMETHODIMP RequestSample(IUnknown* pToken) override;

    // ---- IMFMediaStream2 ----
    IFACEMETHODIMP SetStreamState(MF_STREAM_STATE state) override;
    IFACEMETHODIMP GetStreamState(MF_STREAM_STATE* pState) override;

    // ---- IMFAsyncCallback ----
    IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue) override;
    IFACEMETHODIMP Invoke(IMFAsyncResult* pResult) override;

    // ---- IKsControl (camera control passthrough; minimal) ----
    IFACEMETHODIMP KsProperty(PKSPROPERTY, ULONG, LPVOID, ULONG, ULONG*) override;
    IFACEMETHODIMP KsMethod(PKSMETHOD, ULONG, LPVOID, ULONG, ULONG*) override;
    IFACEMETHODIMP KsEvent(PKSEVENT, ULONG, LPVOID, ULONG, ULONG*) override;

    // Called by the source on Start/Stop/Shutdown.
    HRESULT Start();
    HRESULT Stop();
    HRESULT Shutdown();

    // Exposes the per-stream attributes (stream category/id/shared) that the
    // Frame Server requires to recognise this as a video-capture stream.
    HRESULT CopyStreamAttributes(IMFAttributes** ppAttributes);

    // ---- IMarshal (agility via free-threaded marshaler) ----
    ONYX_AGILE_MEMBERS()

private:
    HRESULT CheckShutdown() const;
    HRESULT DeliverSample(IUnknown* token);

    Microsoft::WRL::Wrappers::SRWLock       _lock;
    Microsoft::WRL::ComPtr<IMFMediaSource>  _parent;
    Microsoft::WRL::ComPtr<IMFMediaEventQueue> _eventQueue;
    Microsoft::WRL::ComPtr<IMFStreamDescriptor> _streamDescriptor;
    Microsoft::WRL::ComPtr<IMFMediaType>    _mediaType;
    Microsoft::WRL::ComPtr<IMFAttributes>   _streamAttributes;

    DWORD           _streamId = 0;
    bool            _shutdown = false;
    MF_STREAM_STATE _state = MF_STREAM_STATE_STOPPED;
    LONGLONG        _nextSampleTime = 0;
    uint64_t        _frameIndex = 0;
};

}  // namespace onyx
