# Third-party components for the Live2D technical preview

CodexBall's own license does not relicense the following dependencies or the official sample models.

## Live2D Cubism Core and sample models

Copyright © Live2D Inc. All rights reserved.

The local preview uses the Windows x64 Core DLL from **Cubism SDK for Native 5 R4.1**. Haru was used in the initial verification and is no longer an application appearance. The current curated appearances use Koharu, Haruto, Mao, and Mark runtime assets from the user-downloaded official sample ZIPs; see [local model integration](docs/imported-live2d-models.md). The SDK archive SHA-256 is `B71EC6BAFC6578CCE3B4CBBAA42A1CB51AE6EB477557932B02D22AF957E733C7`.

- [Official SDK download](https://www.live2d.com/en/sdk/download/native/)
- [Live2D Proprietary Software License](https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html)
- [Live2D Open Software License](https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html)
- [Live2D Free Material License](https://www.live2d.com/eula/live2d-free-material-license-agreement_en.html)
- [Sample model terms](https://www.live2d.com/en/learn/sample/model-terms/)
- [Publication and expandable application licensing](https://www.live2d.com/en/sdk/license/)

The Core DLL is prepared locally by `scripts/Setup-Live2D.ps1`; the four sample models are extracted from local ZIPs by `scripts/Import-Live2DModels.ps1`. These runtime assets are excluded from Git. The import script retains each source ReadMe alongside its model; the individual model conditions continue to apply. Original SDK license notices and the redistributable file list are copied to the app output under `Live2D/Licenses`. This is a local technical verification, not confirmation of publication rights for a model-importing application.

## Community C# adapter

`Live2DCSharpSDK.OpenGL`, `Live2DCSharpSDK.App`, and `Live2DCSharpSDK.Framework` **1.0.0** are third-party adapters, not official Live2D SDKs. Package source: [SamHou0/Live2DCSharpSDK-Fork](https://github.com/SamHou0/Live2DCSharpSDK-Fork), package source commit `b1806e17863efe202b7730283b42d70692d0b877`, based on [Coloryr/Live2DCSharpSDK](https://github.com/Coloryr/Live2DCSharpSDK).

`CodexBall.App/Companion/Live2D/OpenTkApi.cs` adapts the upstream OpenTK API bridge. The adapter repositories declare the following MIT license. Any incorporated Live2D SDK portions retain their original notices and applicable Live2D terms.

The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors
All Rights Reserved

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Rendering dependencies

- [OpenTK 4.9.4](https://github.com/opentk/opentk): MIT, with native GLFW components under their respective notices.
- [SkiaSharp 2.88.9](https://github.com/mono/SkiaSharp): MIT; underlying Skia and bundled dependencies retain their respective notices.
