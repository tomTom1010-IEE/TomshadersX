# Tom Shaders X 0.4.0

## Infra1 分发更新 - 2026-10-28

先安装 [TomRenderInfrastructure 0.1.0](https://github.com/tomTom1010-IEE/TomRenderInfrastructure/releases/tag/v0.1.0)，再安装本次 Shader 包。
Shader 包不再内置 DLL、Probe 或 Feather 辅助资源，统一由基座提供，TomX 与 BSDF 共用一份；请清除重复插件并一起替换 Probe 0.2.1 旧资源。Shader 版本和原 Bundle 字节不变，新标签为 `v0.4.0-infra.1`。可选景深原型单独下载、默认 Native；受控 KKS 回归不等于全角色／GPU 性能认证，下方历史验收保持原记录。

[English](ReleaseNotes.en.md)

面向 KKS、Unity 2019.4 Built-in Forward、D3D11。标签：`v0.4.0-20261024`。
本包替换旧主 Shader zipmod，不要同时安装两个版本。

## 相对 0.3.1 的更新

- 共十二款 Shader，包含独立 MainOpaqueXTess、SkinXTess、MainAlphaXTess。
  Tess 继承普通材质 ID 和共享光照；颜色、附加灯、描边、投影和 Alpha 深度预写
  共用变形。要求 D3D11／SM5；细分倍率 1 仍有 Hull／Domain 开销。
- Skin 可选面部 SDF，使用独立阈值图、区域图和明确的编码选项，不将 DetailMask
  改作 SDF。可选头骨方向插件跟随动画头部，不重置共享材质。
- HairX 双高光瓣、角度偏移、明确的流向约定、局部控制图、独立高光法线，
  以及无需贴图的 Surface Height 偏置。后者偏置反射坐标系，不是世界空间平移。
- 干燥底材 Specular 与 MatCap 分别拥有主／细节法线影响控制。HairX 仅使用
  自身控制一次，非默认影响值下的 horizon 跟随选定法线，保留几何防护；其他层独立。
- MatCapBlendMode 支持 Multiply（0）至 Add（1）连续混合，保留两个端点。
  MatCapColor 默认白色、A=1；A 只控制 MatCap 权重，Add 继续保持 Base-only。
- SkinShadeColor 独立控制 DetailMask G／LineMask B 绘制漫反射阴影，强度仍由
  SkinShadeStrength 控制。Source 0 保留旧 ShadowColor 联动，Source 1 使用独立色；
  模板选择 Source 1。SkinLineColor 与可选 ToonShadeColor 继续独立；SkinShadeColor A 不使用。
- 整理 Hair ME 分类与双语提示，明确单位、共享／主瓣／第二瓣职责、贴图通道
  以及系列之间有意保留的语义差异。
- Head Band 继续封存：九个隐藏兼容字段不再参与计算，不包含旧 HeadBand DLL。

ToonMinLighting 保持 **0.15**，间接漫射增益保持 **0.25**。
不修改场景默认阴影设置，也不声称消除了 Built-in 斜光阴影贴图条纹。

## 兼容与升级

更新前备份角色卡、场景和插件配置。已保存数值及已绑定贴图继续有效。Hair 新效果
默认中性或关闭；局部高光控制图须遵循规定通道与中性值，不复用 HairGloss。
SDF 需要匹配的绘制资产和校准方向，不是仅打开开关。Tess 可能暴露 UV／硬法线
接缝并改变包围盒；需要时使用可选 bounds 插件，与其他网格／包围盒写入插件的兼容需另测。

Skin 缺图采用显式绑定，不自动修复。DetailMask／SkinControlMap 真正为空时，在 ME
导入配套 `Material/SkinDetailNeutral.png`（1,0,0,1）或 `Material/SkinControlNeutral.png`
（1,1,0,1）。运行时空槽仍使用 ShaderLab 黑／白兜底，不等于中性；已有游戏／自绘
贴图应保留。本版不增加缺图插件，也不按尺寸／内容猜测缺图。

请手动移除已安装的旧 `TomHairHeadBand.dll`，包括自定义插件目录中的副本。
HairFeather／SkinSDF／Tessellation 应替换旧文件，不要保留相同插件 GUID 的多个 DLL。

## 完整包与安装

- 0.4.0 主 zipmod：十二个载体、Shader、中性依赖与 ME 提示。
- 沿用已验收的 Tom Lighting Probes 0.2.1 zipmod、`KKS_TomProbe.dll`、
  `TomProbe.Runtime.dll`，内容与上一完整配套包相同。
- `TomHairFeather.dll` 及同目录配套 `hairx-feather.unity3d`。
- 可选 `TomSkinSDF.dll`、`TomTessellation.dll`，两者不需要额外辅助 bundle。
- 双语安装说明、手册／发布说明、AGPL／上游 MIT 声明与 SHA-256 校验值。
  不含测试、报告、缓存，以及本地测试用游戏贴图／模型。

关闭 KKS／CharaStudio，移除被替换的旧 zipmod 与重复插件后，合并包内 `mods`
和 `BepInEx` 到游戏根目录。zipmod 保持压缩。另需 BepInEx 5、Sideloader、
MaterialEditor；不重新分发游戏／Unity 程序集或第三方加载器。仅适用 KKS，不适用 KK。

## 验证范围

发布门禁覆盖全部十二系列、针对性 Unity 回归、严格 bundle 构建／重载和显式成品渲染。
Editor 结果不代表全角色游戏性能认证或任意导入资产兼容性。原展示页保留固定曝光
真实 GPU 图，不将其描述为重新渲染全部新增功能。

[下载](https://github.com/tomTom1010-IEE/TomshadersX/releases/tag/v0.4.0-20261024)
| [用户手册](TomShadersX-UserManual.zh-CN.md)
