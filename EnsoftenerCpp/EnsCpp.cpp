#include "pch.h"
#include "EnsCpp.h"

HRESULT SetComputeShader(ID2D1ComputeInfo* cInfo, GUID guid) { return cInfo->SetComputeShader(guid); }

HRESULT RegisterEffectFromString(ID2D1Factory1* factory, GUID guid, PCWSTR string, D2D1_PROPERTY_BINDING* bindings, UINT bindingLength, PD2D1_EFFECT_FACTORY eFactory)
{ return factory->RegisterEffectFromString(guid, string, bindings, bindingLength, eFactory); }

UINT InteropTest() { return 42; }
