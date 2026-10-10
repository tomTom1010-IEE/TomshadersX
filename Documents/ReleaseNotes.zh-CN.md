# Tom Shaders X：2026-10-11 发布说明

[English](ReleaseNotes.en.md)

发布标签为 `v0.3.1-20261011`。本次日期修订保留 shader 包版本 0.3.1 和已验收的
运行时二进制，替换此前的 0.3.1 主 zipmod，不能与旧包同时安装。

## 更新内容

- HairX 对齐 KKS 的 `_ColorMask`、`_Color`、`_Color2`、`_Color3` 三色染色约定，
  保留独立 BaseColor。没有蒙版时不会自动用 `_Color` 染色已有彩色头发贴图。
- SkinX、不透明及 Alpha 系列共享现代液体层：明确的法线编码、覆盖区域阈值与柔化、
  独立 GGX 湿润反射、粗糙度与 IOR，以及法线强度和抗锯齿控制。Eye/Hair 不加入液体。
  新增第九款 shader LiquidX，用于薄表面液体覆盖层。
- 液体颜色 alpha 独立控制色素透明度，不同时关闭湿润反射。底层衰减与色素阴影颜色
  分离，减轻多余暗边而不关闭场景投影。LiquidEnergyBlend 默认 0.25，
  LiquidAttenuationNormal 默认 0，LiquidShadowColor 的 alpha 默认 0。
- 增加 Toon 法线影响和抗锯齿控制，改善漫反射过渡的不规则边缘，不改变反射法线。
- ToonMinLighting 在八个 shader 及配套材质中默认 **0.15**，设为 0 可关闭。
  它重映射经过背光限制和投影后的主光 Toon 漫反射，自投影和外部遮挡中的暗部都会
  提亮；仅在 Base 计算一次，不随附加灯重复累加。它不是自发光或最终像素亮度下限。
- ToonDarkFillColor 改名为 **ToonShadeColor**，原可选彩色补光行为不变，alpha 默认 0。
  间接漫射默认值仍为 0.25。
- 同步更新中英文手册和 MaterialEditor 提示。

## 兼容性

显式保存的 ToonMinLighting 不会被覆盖；未保存该参数的旧材质会继承 0.15，因此
暗部可能变亮。使用过中间版本 ToonDarkFillColor 的角色卡，需要将原 RGBA 重新填入
ToonShadeColor，本包不含自动角色卡迁移。ShadeColor 不是最低光照的染色参数。

液体实现替换了此前路径，已有液体材质可能需要重新调参。导出的法线 PNG 和游戏原始
液体贴图须按手册选择正确的显式法线编码。

## 完整包内容

- Tom Shaders X 0.3.1 修订版 zipmod，含九款 shader 和主 asset bundle。
- Tom Lighting Probes 0.2.1 zipmod，含工作室 RP/SH 物品和对应 asset bundle。
- TomHairFeather.dll 与配对的 hairx-feather.unity3d。
- KKS_TomProbe.dll 和 TomProbe.Runtime.dll。
- 中英文手册、发布说明、许可证和 SHA256SUMS.txt。

配套插件未改动。探针包保留了 SH 刷新期间继续使用上次漫射信息的修复。

## 安装

1. 退出 KKS/CharaStudio，备份安装目录和配置。
2. 移除旧 Tom Shaders X、Tom Lighting Probes zipmod，以及三个 DLL 和头发 helper
   bundle 的重复副本，包括自定义插件目录中的旧文件。不要同时保留相同 GUID 的两个版本。
3. 将压缩包内 mods 和 BepInEx 目录合并到 KKS 游戏根目录。
4. 不要解压 zipmod；hairx-feather.unity3d 必须与 TomHairFeather.dll 同目录。

需自行安装兼容的 BepInEx 5、Sideloader 和 MaterialEditor；不附带第三方基础加载器
及 Unity/游戏程序集。仅适用于 KKS，不适用于 KK。
用户已完成此次修改的游戏内验收，包内 Unity 检查也已通过；不代表新增整角色性能认证。

[下载发布包](https://github.com/tomTom1010-IEE/TomshadersX/releases/tag/v0.3.1-20261011)
| [用户手册](TomShadersX-UserManual.zh-CN.md)
