# 芙洛拉花园 · Android 发布工程

本分支对应 Unity 源码提交 [60a949f](https://github.com/Xuechun-Hu/floralis-garden/tree/60a949f5f6cbd5466a5c41ce4be972c88c17a403)。Unity 6000.3.25f1 已将场景、全景资源及 IL2CPP 程序编译为 Android ARM64 运行数据。编辑游戏内容请使用 main 分支。

在仓库根目录运行 `gradle --no-daemon assembleRelease`，使用 Gradle 8.11.1、JDK 17、Android SDK 36、Build Tools 36.0.0。通过 ANDROID_HOME 指定 SDK。生成的应用位于 `launcher/build/outputs/apk/release/launcher-release.apk`。

应用标识为 `com.xiaohu.floorgarden`，版本为 0.10.20。平台构建可以通过 Gradle 初始化脚本设置 versionCode。
