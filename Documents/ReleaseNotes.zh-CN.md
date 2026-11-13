# Tom Shaders X 0.4.0

## 2026-11-13 Shader 更新

- 发布手动构建的十二系列 zipmod，包含下述全角度／RGB Ramp、CastShadowColor、
  Skin 默认值校准和可选脸部投影保护。
- 发布标签为 v0.4.0-20261113，包内 Sideloader 版本仍为 0.4.0。
  请替换旧 tom.Shaders.X zipmod，不要保留重复包。
- 本次是 Shader 更新，不是新的完整运行时发行包；保留 Infra1 配套插件和资源。
  不包含或安装新的插件 DLL。
- 沿用此前限定范围的 Unity／Studio 验收；手动重建包完成归档和元数据核对，
  本次没有重新执行 GPU 或游戏内验收，不宣称硬阴影痤疮已根治。

## 未发布：Skin 投影保护收尾 - 2026-11-06

- 保留 SkinX／SkinXTess 的 SkinFaceShadowProtection，作为按 NormalMask B
  设置的可选漫反射接收投影下限。默认仍为 0；0.5 仅是有合适遮罩的脸部调节起点，
  不是新的脸部／身体全局预设。Debug 14 显示实际采样的 B。
- 保留共享光照与已验收默认值，包括 ToonMinLighting=0.15；不自动关闭脸部／
  身体 Caster，不替换用户贴图。
- 该保护同时削弱同一接收区域的自投影、刘海和外部物体投影，不区分投影来源，
  也不修复硬阴影的底层采样伪影。
- Face SDF 继续作为独立可选的 Toon 分界控制，不是阴影痤疮修复器。
  保持 receiveShadows 开启并显式关闭脸部 Renderer 的 Caster，仍可接收刘海投影，
  但会失去脸的全部向外投影，不只是自投影。只有同时保留脸向其他对象的投影，
  却唯独排除自身接收时，才需要另行分离投影来源／接收路径，超出当前材质范围。
  此限定修正不改变默认值或已有测试包。
- 早期嘴角 Toon 突出与后续阴影贴图条纹是不同案例。阈值校准移动前者的分界，
  不修复后者的深度比较或投影几何。
- 现有源码／成品 GPU 检查与受控 Studio 对比支持本次收尾，不代表所有资产或
  性能认证。已交付增量包保留原实验标签；本次仅固化文档，不重新构建、安装或
  发布到 GitHub，不宣称硬阴影条纹已根治。

## 未发布：全角度与 RGB Toon Ramp - 2026-11-05

- 十一款底材 Shader（含 Tess）新增 RampInputMode、RampColorMode，默认仍为
  Legacy／R 通道（0），不迁移旧保存值。
- Full Angle 将有符号 Toon N dot L 映射为背／侧／正光全范围，只取消 Toon 漫反射
  半球截断；正常投影和其他光照波瓣保持独立。
- RGB 复用两张 Ramp 和次级蒙版，忽略 Alpha；程序化 Ramp 仍为灰度。
  ToonMinLighting 保持 0.15，抬升 RGB 响应时保留色相。
- SDF 保持端点归属；液体颜料保持旧标量继承，LiquidX 不变。
  不增加采样器、Pass、关键字或插件。
- 更新 ME 提示和中英文手册，提供可选灰度／彩色 Ramp 示例。
  这是可选测试更新，尚未发布到 GitHub，也不替换独立渲染基座。

## 未发布：投影漫反射染色 - 2026-11-05

- 十一款底材 Shader（含 Tess）与 ME 新增 CastShadowColor RGBA。默认白色、A=0
  为中性；RGB 是有界乘算色，A 控制混合权重。
- 区域跟随重映射后的接收投影与 ShadowStrength。Base 对主光漫反射、Toon 补光／
  最低明暗补偿和间接漫反射各染色一次；Add 仅染当前灯的漫反射。
  不增加次级投影颜色、新贴图、Pass、关键字或插件。
- 高光、反射、Rim、自发光、涂层及液体颜料／反射保持独立。LiquidX 不变。
  ShadowColor 保留旧间接光染色与 Skin 兼容联动，不覆盖已保存参数。
- ToonMinLighting 保持 0.15，位于染色和层衰减之前。正常投射与现有引擎／透明
  接收阴影限制不变。这是源码更新，尚未安装或发布新 Bundle。

## 未发布：Skin Toon 默认值校准 - 2026-11-04

- SkinX、SkinXTess 的 Shader 与模板材质将 ToonThreshold 从 0.5 调整为 0.1；
  ToonSoftness 保持 0.1，ToonMinLighting 保持 0.15。
- 这是脸部与身体共用的全局默认值，不按网格自动切换预设。
  材质、角色卡和场景中显式保存的值保持不变，不增加迁移插件。
- 其他 Shader 默认值、共享光照算法与正常投影不变。校准改善普通侧脸分界，
  不保证所有转头／光向下嘴鼻轮廓都平滑；尚未发布新的 Bundle。

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
