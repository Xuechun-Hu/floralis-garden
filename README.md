# 芙洛拉花园 · Floralis Garden

在 PICO Space Pro 上，从自己的房间走进一座慕夏风格的花园。

1. 戴上头显，看看脚下和附近的墙面，让应用采集空间网格。
2. 拿起面前的种子球向前抛。传送门在落点方向出现，蝴蝶从门中飞出，引你靠近。
3. 按门旁提示捏住芙洛拉，拖动她与轮廓对齐，再松手。门框化为金粉，地上开花，房间渐渐过渡到有溪水和蝴蝶的花园。

芙洛拉用 ML-SHARP 生成的 3D Gaussian Splatting 资产渲染。花园远景使用分块的 16K 全景图，近景包含独立的植物与石头，溪流有流动效果和流水声。

## 打开和构建

- 使用 Unity Hub 安装 **Unity 6000.3.25f1**，勾选 Android Build Support、Android SDK & NDK Tools 和 OpenJDK。
- 用 Unity Hub 打开仓库根目录。首次导入需要联网获取 Packages 中锁定的依赖，包括 PICO Unity SDK。
- 打开 `Assets/Scenes/FloorGarden.unity`。在 **File → Build Profiles** 选择 Android，确认构建场景包含 FloorGarden，再选择 **Build** 或 **Build And Run**。
- 真机体验需要允许空间感知并启用手部追踪。应用的 Android 标识为 `com.xiaohu.floorgarden`。

## 工程入口

交互和空间网格逻辑位于 `Assets/FloorGarden/`，全景分块位于 `Assets/Resources/GardenSphere/`。第三方字体笔画数据的署名与许可位于 `Assets/StreamingAssets/ThirdParty/HanziWriter/`。
