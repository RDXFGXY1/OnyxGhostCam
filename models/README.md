# Models

ONNX face-detection models are bundled here at packaging time. They are **not**
committed to git (see `.gitignore`) because of their size and licensing.

Planned models (final choice is an open decision):

| File | Model | Notes |
|------|-------|-------|
| `blazeface.onnx` | BlazeFace | Fast path — very low latency, single/near faces |
| `<accurate>.onnx` | TBD | Accurate path — more robust, higher cost |

All inference runs **locally** via ONNX Runtime + DirectML — no frame ever leaves
the machine. (The app's only network call is the optional update check.)
