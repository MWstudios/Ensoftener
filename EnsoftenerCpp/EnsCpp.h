#pragma once
#ifdef ENSOFTENERCPP_EXPORTS
#define ENSOFTENER_API __declspec(dllexport)
#else
#define ENSOFTENER_API __declspec(dllimport)
#endif

#include <d2d1effectauthor.h>

extern "C"
{

	ENSOFTENER_API HRESULT RegisterEffectFromString(ID2D1Factory1* factory, GUID guid, PCWSTR string,
		D2D1_PROPERTY_BINDING* bindings, UINT bindingLength, PD2D1_EFFECT_FACTORY eFactory);

	ENSOFTENER_API UINT InteropTest();
}