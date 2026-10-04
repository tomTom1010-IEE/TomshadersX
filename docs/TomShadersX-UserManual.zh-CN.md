# Tom Shaders X 用户手册

适用：Tom Shaders X **0.3.1**，Koikatsu Sunshine，Unity 2019.4 Built-in Forward。
配套：Hair Feather 0.3.0；Tom Lighting Probes 0.2.1。本文以 ME 中不带前导下划线的参数名为准。

本次收尾只验证代码、ME 元数据和 Unity 渲染，不包含新一轮游戏内验收或全角色性能认证。
展示球是可复现的功能示例，不是自动应用到角色上的默认预设。

## 1. 安装与选择

主 zipmod 放入游戏的 `mods` 目录，由 Sideloader / MaterialEditor 注册材质。使用分离后的 V+ LTS，可以与 X 系列共存；不要同时安装仍注册同名 `tom/*` 的旧合并包。

| Shader | 用途与区别 |
| --- | --- |
| `tom/MainOpaqueX` | 通用不透明或镂空表面；不是半透明玻璃 |
| `tom/MainAlphaX` | 常规透明表面；可选 DepthPrepass，默认关闭 |
| `tom/MainAlphaXBackFront` | 背面、正面分层绘制的透明壳；不解决任意交叉网格排序 |
| `tom/MainAlphaX2Pass` | 旧深度预写兼容入口；不是 BackFront，不建议作为新材质首选 |
| `tom/HairX` | 头发镂空、各向异性高光、额前头发 stencil 透明及可选羽化 |
| `tom/EyeWX` | 眼白、眼线等眼部表面，cutout 与 stencil writer；不自动识别网格角色 |
| `tom/EyeX` | 虹膜与瞳孔；局部折射、浅层视差、深度图、可选 RGB 色散 |
| `tom/SkinX` | 脸部与身体共用；保留游戏皮肤、叠加层、服装遮罩和液体约定 |

只使用一般表面、Clearcoat 或 EyeX 光学不需要额外插件。HairX 的 Feather 模式需要 `TomHairFeather.dll` 和同目录 `hairx-feather.unity3d`；缺少支持时回退硬边，不会自动得到柔化 stencil。

工作室探针物品另装 Tom Lighting Probes 的 zipmod，并安装 `KKS_TomProbe.dll` 与 `TomProbe.Runtime.dll` 到 `BepInEx/plugins/TomProbe`。不要保留多份旧 DLL。控制 shader 只给探针物品用，不应替换角色材质。

## 2. 通用调节顺序

1. 选择正确的 shader，保留游戏管理的贴图、颜色和 UV 参数。切换前保留角色卡或场景副本。
2. 关闭所有 Debug View，确认主贴图、透明度和裁剪正常，再调风格。
3. 先调整 Toon Shading 与 Lighting，建立明暗关系。
4. 再调整 Material / Direct Specular，区分普通高光与 Toon 高光。
5. 选择 Environment Reflection 或 MatCap；最后按需加入 Clearcoat、Rim 和 Outline。
6. 眼睛、头发、皮肤的专属功能在通用底层之上调节，不需要推翻 stencil 或透明度设置。

同名公共控件在八个 shader 中保持相同类别、类型和范围。游戏兼容别名有意保留原拼写，包括 `SpeclarHeight`。ME 将部分枚举显示成数值滑条时，请输入说明中的**整数**，不要停在两个模式之间。贴图、数值、颜色仍使用 ME 原有控件；本文不假定所有版本都支持自定义下拉菜单。

`IndirectDiffuseIntensity` 新材质默认 **0.25**。它只缩放间接漫射，不缩放直接灯、反射或 Clearcoat。已存储的旧值不会自动改成 0.25。Maker 中皮肤显得发白时，应先检查间接光和曝光，不要立即重命名游戏颜色参数。

### 常用起点

以下是调试起点，不是保证适合所有角色的预设。保持灯光不动，每次只调一组。

| 目标 | 参数起点 |
| --- | --- |
| 连续明暗 | `UseRamp=0` |
| 明确二段阴影 | `UseRamp=1`、`RampMode=0`、`ToonThreshold=0.5`、`ToonSoftness=0.05` |
| 柔和 Toon 过渡 | 在上项基础上提高 `ToonSoftness`，而不是只提高间接光 |
| 连续 GGX 高光 | `SpecularToonBlend=0`，调 `Roughness` |
| Toon 高光 | `SpecularToonBlend=1`，调 `SpecularSize/Threshold/Softness`；`SpecularBands` 输入 1–4 |
| 湿润涂层 | `ClearCoat=0.5`、`ClearCoatRoughness=0.12`、`ClearCoatIOR=1.5` |
| 虹膜浅凹 | `EyeOpticsMode=1`、`IrisDepth=0.08`、`EyeOpticsStrength=1`，再从侧面观察 |

## 3. 光照与风格

**Toon Shading**：`UseRamp` 混合连续漫反射与 Toon 明暗。`RampMode=0` 使用程序化阈值，1 使用 RampTex 的 R 通道；SecondaryRamp 通过 LayerMask R 和 SecondaryToneStrength 混合。RampTex 是明暗曲线，不是直接彩色渐变染色。

**阴影**：ShadowStrength 调漫反射阴影参与度；ShadowRemapStrength / Threshold / Softness 重映射阴影可见度。UseRampForShadows 控制阴影进入 Toon 曲线。外部遮挡与曲面自身背光不是同一件事。Clearcoat 直接高光仍受物理阴影约束，不应把它当作无阴影发光。

**直接高光**：SpecularStrength 是强度，Roughness 是连续高光粗糙度，SpecularSize 是 Toon 高光大小，两者不互相替代。SpecularIOR/FresnelStrength 控制底层介质响应；Metallic 增大时底层漫反射减少。SpecularAA 用于缓和法线与高光边缘闪烁，不能替代所有时域抗锯齿。

**多灯**：MainLightIntensity / AdditionalLightIntensity 分别调主灯和逐像素附加灯。直接漫射、高光与 Clearcoat 会响应附加灯；SH、环境反射、加法 MatCap、Rim、发光不逐灯重复添加。乘法 MatCap 仍调制各灯的漫反射。

**间接漫射**：LightProbeBlend 从 AmbientColor 过渡到 Unity 的 SH/Light Probe 路径；CustomSHVolumeBlend 控制已就绪的自定义 SH Volume。IndirectToonBlend 可风格化间接明暗，但不能凭空补出方向信息。没有就绪的 Volume 时保留内置间接光路径。

**环境反射**：ReflectionMode：0 关闭，1 MatCap，2 环境，3 两者。环境模式需要可用的探针或天空反射；EnvironmentIntensity、Roughness、ToonBlend 分别控制强度、模糊和风格化。默认 UseMaterialRoughnessForEnvironment=1 时，环境采用材质粗糙度，独立 EnvironmentRoughness 不接管。Box Projection 适合有限室内空间，但不是光线追踪。

**MatCap**：视角空间的二维外观贴图，不是场景实时反射。MatCapBlendMode：0 乘法，1 加法。MatCapNormalSource：0 网格法线，1 主法线，2 主法线加细节。

**Rim / Outline / Emission**：Rim 是视角边缘项；Outline 是外扩壳轮廓，两者独立。屏幕空间 OutlineWidth 按像素调宽。发光不会自动给周围物体投光。SkinX 的 UV4 用于游戏叠加层，不能再用作外轮廓平滑法线。

## 4. 贴图通道速查

颜色贴图按颜色导入，数据贴图关闭 sRGB。法线贴图按 Unity 法线贴图方式导入；不要把普通 RGB 法线图、RG 流向图或灰度深度图互换。在 ME 运行时导入时，先确认该版本的导入行为；本次未在游戏里验证它是否保留所有颜色空间设置。

| 贴图 | 通道定义 |
| --- | --- |
| MainTex | RGB 颜色；A 覆盖率，具体行为取决于 shader |
| MaterialMap | R 金属、G 粗糙度、B AO、A 直接高光遮罩；UsePackedMaterialMap=1 时接管分离图 |
| LayerMask | R 次级 Toon、G Rim、B MatCap、A 环境反射 |
| MetallicMap / RoughnessMap | R 乘相应标量；粗糙度之后再加 RoughnessBias |
| OcclusionMap | R AO；白色无遮挡，影响间接项，不直接关闭主灯 |
| SpecularMask | R 直接高光，不控制环境、MatCap、Rim；packed 模式忽略此图 |
| ReflectionMask | R 公共反射区域，再乘 LayerMask B/A |
| ClearCoatMap | R 涂层权重，G 涂层粗糙度乘数；B/A 不用，白色中性 |
| StrandDirectionMap | 线性 RG，解码到 [-1,1] 的切线空间轴向；不是 Unity 法线图 |
| EyeSurfaceMap | 默认灰度高度，或 R 深度 / A 范围，见眼睛章节 |

一部分贴图共享 sampler 以遵守 Unity/D3D11 的采样器预算；共享的是过滤与 wrap 状态，不代表它们都共享 ST。尤其 SkinX 的 UV0 遮罩应采用相容的 atlas 导入设置。EyeSurfaceMap 使用最终虹膜 UV，不支持独立 ST。

## 5. Clearcoat

Clearcoat 是覆盖在底材上的无色介质高光与环境反射层，不是视差。视差改变看到的内部纹理位置，Clearcoat 表达外表面的光泽，因此可以同时启用。

- `ClearCoat=0` 关闭；不改变 alpha 或 stencil。
- `ClearCoatRoughness` 与底层 Roughness 独立；值越低，高光越集中。
- `ClearCoatIOR` 只影响涂层 Fresnel 和分层能量。设为 1 消除涂层反射，**不改变 EyeX 视差位移**。
- `ClearCoatEnvironmentStrength` 独立于底层 ReflectionMode，因此关闭底层反射并不关闭涂层反射。
- `ClearCoatEnergyBlend=0` 保留 Toon 漫反射亮度，1 使用分层衰减近似；底层镜面项仍受涂层衰减。
- `ClearCoatNormalSource`：0 网格，1 主法线，2 主加细节，3 独立涂层法线。EyeX 的界面法线取自未视差偏移的表面。

涂层很亮时会在视觉上盖住虹膜对比度，但这不等于视差算法被关闭。先关闭涂层或查看 EyeDebugView 的位移图来区分两者。

## 6. HairX

StrandAngle：U 方向为 0°，V 为 90°。StrandDirectionBlend 混合手动方向与流向图；HairAnisotropy 控制条带形高光。当前是单高光瓣，不是双层头发散射或各向异性环境反射。

HairFrontMode：0 关闭，1 硬 stencil，2 内侧羽化。HairFrontOpacity=1 为不透明。stencil 外仍为正常头发 cutout；改变相机侧透明度不会挖掉头发投下的阴影。

Feather Width Mode：0 像素，1 投影世界单位。像素模式保留旧材质行为；世界单位随距离/FOV/分辨率投影。宽度、Midpoint、Power 独立。过大的投影半径受提供器 256 像素预算限制，日志会标注。

眼部 writer 必须先于头发绘制。常用队列是 EyeWX 2472、EyeX 2474、HairX 2475；不要为了让某一层透出而随意打乱队列。羽化只认识受支持的 writer，并不是任意 shader 的真实 stencil 拷贝。眼白缺少支持时，眼白与瞳孔接缝可能被错误当成边界；改为受支持的 EyeWX/EyeWPlus 并检查队列。Mode 1 可作为定位辅助，但不是证明羽化整体正确。

当前羽化提供器针对单眼 D3D11 Forward；立体相机等不支持配置回退硬边。

## 7. EyeWX 与 EyeX

EyeWX 使用游戏 `_Color` 别名，0.5 为中性，再叠加 BaseColor。EyeX 保留原生虹膜旋转、表情层和高光层；这些游戏别名不应被用户手工清空。表面高光贴图和 Clearcoat 可以并存，但叠得过强会显得重复。

AlphaMask / Alpha / Cutoff 控制未偏移源覆盖；StencilCutoff 额外控制 stencil 写入。EyeWX 对通过阈值的区域按 cutout 绘制，EyeX 对存活覆盖继续 alpha 混合。**光学范围、透明范围、stencil 范围是三件事。**

### 程序化视差

EyeOpticsMode=1 开启。IrisCenterX/Y 与 IrisRadiusX/Y 定义最终 MainTex UV 中的椭圆。IrisDepthShape：0 原始平底，1 浅锥，2 浅碗。IrisDepth 是两种模式共用的全局凹陷增益，以局部虹膜半径为尺度，不是米或像素。EyeOpticsStrength=0 回到原采样。

EyeRefractionIOR 与 ClearCoatIOR 独立；增加折射 IOR 会减小横向视差，这属于当前光线路径的设计。IOR=1 去掉折射弯曲，不消除凹陷自身的视差。旧材质要复现分离前的结果，可将原 ClearCoatIOR 数值手动复制到 EyeRefractionIOR 一次；系统不强制迁移已保存材质。

### 自绘深度与范围

UseEyeSurfaceMask=1 后，贴图替代程序化椭圆边界和 EdgeFade；与主虹膜贴图及其旋转/ST 对齐。

| EyeSurfaceMapMode | 定义 | 无效果区域 |
| --- | --- | --- |
| 0 灰度高度 | 白=表面/零深度，黑=最深；忽略 A | 涂成白色 |
| 1 R 深度 + A 范围 | R 白=最深、黑=零深度；A 白=范围内 | A=0，并在边缘平滑过渡 |

两个模式都乘 IrisDepth；半径在贴图模式仍作为深度/位移尺度标定，但不再裁剪自绘区域。A 只影响深度和进入过渡，不改变表面透明度。

适合：平底浅凹、浅锥、浅碗、平滑单值灰度场。不要将绘画高光直接转换为深度，更不要把眼睛颜色亮暗当作几何高度。避免陡直坑壁、高频噪声和多层回折表面。

EyeOpticsMaxOffset 限制极端位移；EyeUVMin/Max 是安全图集边界。偏移采样超出安全区域或几何条件失效时会回到原 UV，所以不能只靠无限拉大强度获得深洞。

### 色散与角度

EyeDispersion=0 单路，正值采用三条 RGB 光线的几何近似。它不是波动光学，不会把整个涂层变成彩虹。通常只在高对比边缘和斜视时较清楚。

测试角度时要真正改变相机相对眼表面的方向；仅移动构图或使用始终朝向相机的眼球，差异可能很弱。先用明显纹理与浅锥/浅碗测试，再逐步调整 Depth、IOR、最大偏移和安全范围。

当前不是 POM，不保证近侧坑壁挡住远侧内容。以后确实需要这种自遮挡时，再考虑 **POM / 首次可见交点搜索**；角度差异不够强本身不是必须升级的理由。不需要额外角膜网格。

## 8. SkinX 标准角色贴图

游戏已经将主皮肤颜色合成到 MainTex；BaseColor 是额外乘色，不应重复替代游戏合成过程。ColMask 是可选额外分区染色，不是游戏贴图合成器的 ColorMask。

| 输入 | 作用 |
| --- | --- |
| ColMask + Col0..3 | 先从 Col0 按 R 混合 Col1，再按 G 混合 Col2、B 混合 Col3 |
| DetailMask | R 画入高光/高光门控，G 画入阴影，B 抑制 Rim/Outline，A 区分皮肤与指甲/嘴唇光泽 |
| LineMask | R 内部线条，G 游戏线宽指数，B 细节阴影；A 不用 |
| NormalMask | G 可用来减弱漫射法线细节；不是普通法线贴图，也不继承旧版 B 阴影兜底 |
| overtex1 | UV1（Unity UV2），顶点 R 控制；身体乳头/脸部嘴唇等，支持 tex1mask 的旧 RG 编码 |
| overtex2 | UV2（Unity UV3），顶点 B 控制；身体对应层或脸部动态腮红 |
| overtex3 | UV3（Unity UV4）；脸部眼影等，身体含义依资产而定 |
| AlphaMask | 服装 R/G 遮罩，由 alpha_a/b 激活；阈值固定 0.5 |
| SkinControlMap | R 柔化，G 暖过渡，B 自绘湿润区域；不是原版 DetailMask 的替代品 |

DetailNormalMapScale 还参与游戏内部线条/画入阴影；SkinDiffuseNormalDetail 只减弱漫反射细节，保留高光/涂层细节。SkinStrength / Wrap / Warmth 提供前侧柔化和明暗交界暖色，不是屏幕空间或体积 SSS，也不会穿透外部遮挡。

SpecularPower / SpecularPowerNail 保留游戏皮肤与指甲/嘴唇的光泽增益；SkinGameGloss 控制参与度。它们不等于 Roughness，也不自动调 Clearcoat。SkinPatternStrength / notusetexspecular 控制移动的画入高光遮罩。

### 液体与湿润

`liquidmask` 的 RGB 组合编码游戏五个部位；`liquidftop/fbot/btop/bbot/face` 为各部位 0–2 的量。`Texture2` 的 R/G 是第一、第二阶段液体覆盖图；`Texture3` 是液体切线法线，不是深度或颜色贴图。这里说的是最终 Skin 材质槽，不能与游戏合成器中的同名槽混为一谈。

LiquidTiling 在 ME 因兼容性以颜色控件呈现，但它是原始向量：R/G=UV 偏移，B/A=UV 缩放，默认 `(0,0,1,1)`，不要按颜色做 gamma 调整。

SkinLiquidColorStrength 控制液体底色；SkinLiquidMaterial 混合底层粗糙度。SkinCoatCoverage：0 普通涂层图，1 游戏液体，2 自绘湿润，3 二者并集。它只限制 Clearcoat 的覆盖，必须同时启用 ClearCoat。SkinWetness 乘 SkinControlMap B。

## 9. Reflection Probe 与 SH Volume

在工作室 Tom Lighting > Probes 添加物品，通过 ME 编辑其控制材质。256/512/1024 或不同 SH 网格是起点，不是锁定版本；无需删物品重导。参数由插件读取，shader 本身不执行捕捉。

| 常用控件 | 含义 |
| --- | --- |
| ProbeEnabled / Intensity | 启用与贡献强度；强度变化不重捕捉 |
| ProbeRefresh | 0→1 或 1→0 都请求一次；保持 1 不会每帧刷新 |
| ProbeRefreshScope / Group | 范围 0 自己、1 同组、2 全部；Group 为整数 |
| ProbeUpdateMode | 0 加载、1 位置变化、2 定时、3 手动 |
| ProbeCaptureOnLoad | 初次或重新启用时捕捉，与上述模式独立 |
| ProbeInterval | 请求间隔秒数；RP 最小 0.5，SH 最小 5，不保证这时已捕完 |
| ProbeSize / Offset X/Y/Z | 世界轴对齐范围与偏移，使用这些参数而非缩放/旋转物品 |
| ProbeNearClip / FarClip | 六个面各自的裁剪平面；NearClip 不是球形排除半径 |
| ProbeCapturePreset | 0 环境层，1 含角色，2 自定义 32 位 layer mask |
| ProbeQuality | RP：0=256、1=512、2=1024，每个 cubemap 面 |
| SHGridX/Y/Z | 每轴 2–8，总采样点是三者乘积 |
| SHCaptureQuality | 0/1/2/3 分别为每面 8/16/32/64 |
| SHSamplesPerFrame | 每帧请求 1–4 点，同时受插件全局预算限制 |

RP 提供镜面环境反射；SH 提供低频方向性漫射，不能替代尖锐高光或实时阴影。自定义 SH 一次为一个 renderer 选择一个体积，依据 bounds 中心、优先级与边缘权重；不是多个体积的逐像素混合。

捕捉方向保持世界轴，不随物品旋转。父对象旋转若改变物品世界位置，仍会造成真正的位置变化。近裁剪只能裁近处几何；要避免角色自捕捉，优先检查捕捉层。被放到特殊层的角色不保证由环境预设自动排除。

共用队列每次只做一个捕捉任务；同组刷新先 SH 后 RP。SH 刷新完成并重新绑定前保留旧 atlas 与匹配的范围；首次没有历史结果时走内置间接光。RP 更改分辨率/HDR 时会重建内部 helper，期间可能回退其他探针/天空。

捕捉结果不随场景持久化，只保存参数；重新加载需要重捕捉。SH 每个采样点渲染六面并同步读回，分帧不是免费异步 GI；复杂场景仍可能卡顿。RP 需要 Unity 的 Realtime Reflection Probes 质量选项，插件不会擅自修改全局质量。

## 10. 排查与已知边界

| 现象 | 优先检查 |
| --- | --- |
| 颜色发白 | 间接漫射/环境强度、Clearcoat、曝光、是否重复高光；新默认间接为 0.25 |
| 调粗糙度没变化 | 当前是否 Toon 高光；环境是否使用材质粗糙度；贴图 G 是否为零 |
| 没有涂层反射 | ClearCoat、IOR 是否为 1、涂层覆盖图、SkinCoatCoverage、可用环境 |
| 视差不明显 | 相对视角、Optics 开关、Depth/Strength、IOR、自绘数据颜色空间、边界回退 |
| 眼边羽化出现内缝 | 支持的眼白/瞳孔/眼线 writer 是否齐全，queue 是否早于头发 |
| 透明物体互相穿插不对 | render queue、网格交叉和深度写入；BackFront 不是 OIT |
| SH 没反应 | 插件与物品、是否完成捕捉、体积范围、CustomSHVolumeBlend、是否在 Debug View |
| 全画面变成遮罩色 | Eye/Skin/Clearcoat/Lighting Debug 是否归零 |

保留现有效果的后续项：EyeX 多灯路径定向优化（非当前阻塞）；真实自遮挡触发时才评估 POM；HairX 双高光瓣等另行设计；EyeWX 眼白与眉毛/眼线拆分暂缓。探针捕捉预算、全角色与多角色的游戏 GPU 性能需另行实测，不从标准球推断。

## 11. 展示图与开发证据

配套展示包提供逐项标准球对照、视差多角度图、单张 PNG、生成贴图以及记录所有材质参数的可展开表格。同一组对照使用相同相机、灯光和固定显示转换；需要变化的相机/光照明确写在图名与记录中。SH 展示使用已知系数场隔离漫射效果，探针捕捉刷新正确性由独立 Unity 运行时回归验证。

源码安装与打包方法见源码仓库的 Documents/Development.md。
