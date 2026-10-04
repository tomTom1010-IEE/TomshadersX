#include <Windows.h>
#include <d3d11.h>
#include <atomic>

// Editor-only timestamps. The immediate context is touched only in render events.
static ID3D11Device* device;
static ID3D11DeviceContext* context;
static ID3D11Query *startQuery, *endQuery, *disjointQuery, *statisticsQuery;
static std::atomic<int> status{0};
static bool statisticsEnabled;
static double milliseconds;
static D3D11_QUERY_DATA_PIPELINE_STATISTICS statistics;

template<class T> static void Release(T*& value) { if (value) value->Release(); value = nullptr; }
static void Cleanup()
{
    Release(startQuery); Release(endQuery); Release(disjointQuery); Release(statisticsQuery);
    Release(context); Release(device);
}

extern "C" __declspec(dllexport) int __cdecl Initialize(void* texture)
{
    if (device || !texture) return 0;
    static_cast<ID3D11Texture2D*>(texture)->GetDevice(&device);
    if (!device) return 0;
    D3D11_QUERY_DESC desc = {D3D11_QUERY_TIMESTAMP, 0};
    HRESULT hr = device->CreateQuery(&desc, &startQuery);
    if (SUCCEEDED(hr)) hr = device->CreateQuery(&desc, &endQuery);
    desc.Query = D3D11_QUERY_TIMESTAMP_DISJOINT;
    if (SUCCEEDED(hr)) hr = device->CreateQuery(&desc, &disjointQuery);
    desc.Query = D3D11_QUERY_PIPELINE_STATISTICS;
    if (SUCCEEDED(hr)) hr = device->CreateQuery(&desc, &statisticsQuery);
    if (FAILED(hr)) { Cleanup(); return 0; }
    status.store(0, std::memory_order_release);
    return 1;
}

extern "C" __declspec(dllexport) void __cdecl SetStatistics(int enabled) { statisticsEnabled = enabled != 0; }

static bool Wait(ID3D11Query* query, void* data, UINT bytes)
{
    const ULONGLONG deadline = GetTickCount64() + 5000;
    HRESULT hr;
    while ((hr = context->GetData(query, data, bytes, D3D11_ASYNC_GETDATA_DONOTFLUSH)) == S_FALSE)
    {
        if (GetTickCount64() > deadline) return false;
        Sleep(0);
    }
    return hr == S_OK;
}

static void __stdcall RenderEvent(int eventId)
{
    if (eventId == 2) { Cleanup(); status.store(0, std::memory_order_release); return; }
    if (!device) { status.store(-1, std::memory_order_release); return; }
    if (!context) device->GetImmediateContext(&context);
    if (eventId == 0)
    {
        status.store(0, std::memory_order_release);
        statistics = {};
        context->Begin(disjointQuery);
        if (statisticsEnabled) context->Begin(statisticsQuery);
        context->End(startQuery);
    }
    else if (eventId == 1)
    {
        context->End(endQuery);
        if (statisticsEnabled) context->End(statisticsQuery);
        context->End(disjointQuery);
        context->Flush();
        D3D11_QUERY_DATA_TIMESTAMP_DISJOINT disjoint = {};
        UINT64 begin = 0, end = 0;
        if (!Wait(disjointQuery, &disjoint, sizeof(disjoint)) ||
            !Wait(startQuery, &begin, sizeof(begin)) || !Wait(endQuery, &end, sizeof(end)) ||
            (statisticsEnabled && !Wait(statisticsQuery, &statistics, sizeof(statistics))))
        { status.store(-2, std::memory_order_release); return; }
        if (disjoint.Disjoint || !disjoint.Frequency || end < begin)
        { status.store(-3, std::memory_order_release); return; }
        milliseconds = double(end - begin) * 1000.0 / double(disjoint.Frequency);
        status.store(1, std::memory_order_release);
    }
}

extern "C" __declspec(dllexport) void* __cdecl GetRenderEvent() { return reinterpret_cast<void*>(&RenderEvent); }
extern "C" __declspec(dllexport) int __cdecl Result(double* ms, UINT64* ps, UINT64* vs, UINT64* primitives)
{
    int ready = status.load(std::memory_order_acquire);
    if (ready == 1)
    {
        *ms = milliseconds; *ps = statistics.PSInvocations;
        *vs = statistics.VSInvocations; *primitives = statistics.IAPrimitives;
    }
    return ready;
}
