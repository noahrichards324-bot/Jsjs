#ifndef HORIZON_UWP_ENGINE_BRIDGE_H
#define HORIZON_UWP_ENGINE_BRIDGE_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define HORIZON_UWP_ENGINE_BRIDGE_ABI_VERSION 2u
#define HORIZON_UWP_INPUT_QUEUE_CAPACITY 4096u

typedef struct HorizonUwpInputEvent {
    uint32_t kind;
    uint32_t code;
    uint32_t state;
    float x;
    float y;
} HorizonUwpInputEvent;

enum HorizonUwpInputEventKind {
    HORIZON_UWP_INPUT_NONE = 0,
    HORIZON_UWP_INPUT_KEY = 1,
    HORIZON_UWP_INPUT_MOUSE_BUTTON = 2,
    HORIZON_UWP_INPUT_MOUSE_MOVE = 3,
    HORIZON_UWP_INPUT_MOUSE_WHEEL = 4,
    HORIZON_UWP_INPUT_GAMEPAD_BUTTON = 5,
    HORIZON_UWP_INPUT_GAMEPAD_AXIS = 6,
    HORIZON_UWP_INPUT_FOCUS = 7,
    HORIZON_UWP_INPUT_RESIZE = 8,
    HORIZON_UWP_INPUT_TEXT = 9
};

uint32_t horizon_uwp_engine_bridge_abi_version(void);
uint32_t horizon_uwp_input_event_size(void);
uint32_t horizon_uwp_input_pending(void);
int32_t horizon_uwp_input_push(uint32_t kind, uint32_t code, uint32_t state, float x, float y);
int32_t horizon_uwp_input_pop(HorizonUwpInputEvent *out_event);
void horizon_uwp_input_clear(void);

/* SwapChainPanel rendering host. panel is an ISwapChainPanelNative COM pointer. */
void *horizon_uwp_surface_create(void *panel, uint32_t width, uint32_t height);
int32_t horizon_uwp_surface_last_error(void);
int32_t horizon_uwp_surface_resize(void *host, uint32_t width, uint32_t height);
int32_t horizon_uwp_surface_present(void *host);
void horizon_uwp_surface_destroy(void *host);

/* Link anchor only; do not call until the UWP-native event loop is implemented. */
uintptr_t horizon_uwp_engine_entrypoint_address(void);

#ifdef __cplusplus
}
#endif

#endif
