# NGUInjector 中文兼容版 4.1.7-cn.5

本版本基于原项目 NGUInjector 4.1.7 制作，面向 Steam 版 NGU IDLE 汉化环境。

## 下载

发布包：

```text
package/NGUInjector-Chinese-compatible-v4.1.7-cn.5.zip
```

SHA-256：

```text
02517eded533eae049c3da576f1fde1c11724055ccd61befd64c415f6ef2889c
```

## 使用

1. 启动 NGU IDLE 并进入游戏。
2. 解压 `NGUInjector-Chinese-compatible-v4.1.7-cn.5.zip` 到全新目录。
3. 运行 `dist/inject.bat`。
4. 注入成功后按 `F1` 打开中文设置界面。

## 主要改动

- 设置界面、悬浮层、快速保存/读取提示、本地错误提示改为中文。
- 将 Augmentation/Augment 相关显示统一译为“挂件”。
- 修复黄金狙击理论最佳区域未解锁时一直等待的问题，会回退到当前已解锁且可打的最高非泰坦区域。
- 新增中文使用逻辑说明，覆盖功能优先级、换装锁、黄金管理、钱坑、任务、许愿、卡牌等实际运行逻辑。
- 区域名称、卡牌稀有度、MacGuffin 类型、动作锁定名称改用汉化术语。
- 保留内部 JSON 配置键名，兼容原项目配置文件。
- 解决 Unity/Mono 下 `System.Resources.Extensions.DeserializingResourceReader` 初始化失败问题。
- 移除 `System.Xml.Linq` 等不必要运行时依赖。
- 发布包仅包含 `NGUInjector.dll`、`SharpMonoInjector.dll`、`smi.exe`、`inject.bat` 和示例配置。

## 排错

请不要把新版文件覆盖到旧目录中，建议每次解压到全新目录。若 `F1` 初始化失败，请查看：

```text
%UserProfile%\AppData\LocalLow\NGUInjector\logs\debug.log
```
