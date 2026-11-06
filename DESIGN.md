# Sleepy HDD 设计文档

手动控制内置机械硬盘休眠/离线的 Windows 工具，revoSleep 的现代化替代。技术栈：.NET 10 + WinUI 3（unpackaged），仅支持 Windows 11。术语见 [GLOSSARY.md](./GLOSSARY.md)，关键决策见 [docs/adr/](./docs/adr/)。

## 1. 范围

**做**：受管磁盘（内置固定 HDD，`SeekPenalty=1` 且总线为 ATA/SATA/SCSI）的手动休眠、离线、唤醒；保护模式（休眠期间拒绝访问、磁盘保持沉睡）；定时策略（指定时间把指定盘转入指定状态）；开机自启（可选，默认关）。

**不做（v1 明确排除）**：USB 移动硬盘、NVMe/SSD、系统盘；闲置超时自动休眠；访问即自动唤醒；温度/SMART 监控；记录"哪个进程试图访问"（仅报告"谁阻止了休眠"）；非提权 UI + 提权服务的两进程架构；Win10 支持。

## 2. 核心功能实现

### 2.1 磁盘枚举与识别

- `SetupDiGetClassDevs(GUID_DEVCLASS_DISKDRIVE)` 枚举磁盘设备，取 `\\.\PhysicalDriveN` 句柄。
- `IOCTL_STORAGE_QUERY_PROPERTY`（`StorageDeviceProperty`）读 `BusType`、序列号、型号、容量；`IOCTL_STORAGE_GET_DEVICE_NUMBER` + 各卷 `IOCTL_STORAGE_GET_DEVICE_NUMBER` 建立 磁盘→卷 映射。
- 判定受管磁盘：`SeekPenalty=1`（`IOCTL_ATA_PASS_THROUGH` 读 IDENTIFY DEVICE，或按 BusType+型号启发式）且非 USB（`BusTypeUsb` 排除）。
- 判定受限：启动/系统卷所在盘、承载 pagefile 的盘（读 `HKLM\...\Memory Management` 的 `PagingFiles`）、动态盘/Storage Spaces 成员（`IOCTL_STORAGE_QUERY_PROPERTY` 的 `StorageAdapterProperty` + `GET_LENGTH_INFORMATION`/分区类型 `IOCTL_DISK_GET_DRIVE_LAYOUT_EX`）、ATA pass-through 不可用的盘（打开 RW 句柄或试探 SMART 失败）。受限盘在 UI 可见、标注原因、无动作按钮。
- 设备变化：监听 `WM_DEVICECHANGE`，增删磁盘卡片。

### 2.2 状态机

```
在线 ──休眠──▶ 休眠 ──追加离线属性──▶ 离线
在线 ──休眠+离线属性（一步）────────▶ 离线
休眠/离线 ──唤醒──▶ 在线
```

任意两态可直接转换，引擎内部按步骤顺序编排（休眠→离线 = 保持锁定 + 清停转歧义 + 置离线属性；离线→休眠 = 清离线属性、保持锁定）。

### 2.3 转休眠（保护模式，参考 ADR-0002）

对该盘每个卷：
1. `FSCTL_LOCK_VOLUME`（同步重试约 3 秒）；失败 → 中止并用 Restart Manager（`RmGetList`）列出占用进程，托盘通知，回滚已锁定的卷。
2. `FSCTL_DISMOUNT_VOLUME`。
3. `IOCTL_ATA_PASS_THROUGH`：CACHE FLUSH EXT (0xE7) → STANDBY IMMEDIATE (0xE0)。
   - 必须同步调用；绝不用 SLEEP (0xE6)（用户态无法唤醒）。
4. 记录状态到状态文件。

效果：盘符可见，新访问返回拒绝访问，I/O 到不了磁盘，盘保持沉睡。

### 2.4 转离线

执行 2.3 全部步骤后追加：
5. `IOCTL_DISK_SET_DISK_ATTRIBUTES`，`DISK_ATTRIBUTE_OFFLINE` 置位。

效果：卷从系统消失。离线属性持久，由生命周期规则兜底（ADR-0005）。

### 2.5 唤醒

- 从休眠：`FSCTL_UNLOCK_VOLUME`（关闭卷句柄即隐式解锁亦可）→ 下次访问自然 spin-up（ATA 规范：STANDBY 态收到任何命令自动启动）。
- 从离线：清 `DISK_ATTRIBUTE_OFFLINE` → 卷自动重新挂载、盘符回来 → 下次访问 spin-up。
- 可选：唤醒后立即对卷发一次轻量读，让起转发生在用户点击时而非首次访问时（体验优化，默认开）。

### 2.6 定时策略

进程内定时器（非 Windows Task Scheduler 触发，任务计划仅用于开机自启拉起应用）。触发时执行与手动完全相同的流程；按 2.3 锁不住 → 跳过本次 + 托盘通知，不重试。

### 2.7 生命周期（ADR-0005）

- 启动：读状态文件（按磁盘序列号/唯一 ID 匹配）→ 把记录在案且仍离线的盘清离线属性、上线卷。
- 退出（含托盘"退出"）：把本软件置为休眠/离线的盘全部唤醒，再退出。
- 崩溃：下次启动由启动恢复兜底。

### 2.8 运行时与权限

- 应用清单 `requireAdministrator`（ADR-0003）。
- 启动时 bootstrapper 检测 Windows App Runtime，缺失则引导安装（ADR-0004）。
- 开机自启：注册任务计划程序任务（登录触发 + 最高权限），软件内开关负责注册/注销（Q15-A）。

## 3. 架构分层

```
SleepyHdd.sln
├─ src/SleepyHdd.App/        WinUI 3 应用（unpackaged）
│   Views/  ViewModels/  (CommunityToolkit.Mvvm)
│   TrayIcon (H.NotifyIcon.WinUI)  单实例 (AppInstance)
├─ src/SleepyHdd.Core/       无 UI 依赖，可单测
│   DiskInventory   枚举/识别/设备变化（2.1）
│   DiskStateMachine  转换编排、按盘串行、跨盘并行（2.2–2.5）
│   Scheduler       定时策略（2.6）
│   Lifecycle       启动恢复/退出唤醒（2.7）
│   Store           settings.json + state.json（%LOCALAPPDATA%\SleepyHdd）
└─ src/SleepyHdd.Native/     全部 P/Invoke 与 IOCTL 结构体
    AtaPassThrough / VolumeOps / DiskAttributes / StorageQuery / RestartManager
```

- 依赖方向：App → Core → Native。Core 不引用 Windows.UI/WinUI 类型，UI 通过事件/快照订阅状态。
- Native 层是唯一允许出现 unsafe 与 DeviceIoControl 的地方，便于审计。
- 目标框架 `net8.0-windows10.0.22621.0`；`PlatformsTargetFramework` 限定 x64。

## 4. 界面规格

### 4.1 主窗口

- 顶部条：全局按钮（文案跟随默认动作设置："全部休眠"/"全部离线" + "全部唤醒"，Q20-A）。
- 磁盘卡片列表（每盘一张）：
  - 型号、容量、盘符与卷列表（盘符+卷标+用量）、状态徽章：`在线`（绿）/`休眠`（黄）/`离线`（灰）/`受限-原因`（红字说明）。
  - 卡片右侧**单个**动作按钮（Q19-A）：
    | 当前状态 | 按钮 |
    |---|---|
    | 在线 | 休眠 或 离线（按默认动作设置） |
    | 休眠 | 唤醒 |
    | 离线 | 唤醒 |
    | 受限 | 无按钮 |
- 左下角：设置图标 → 进入设置页。
- 无受管磁盘时：空态插画 + 文案。

### 4.2 设置页

- 默认动作：单选（休眠/离线）。
- 定时策略：列表 + 新增对话框（磁盘、时间、星期、目标状态）。
- 开机自动启动：开关（注册/注销任务计划）。
- 关于。

### 4.3 托盘

- 图标状态：任一磁盘休眠/离线时显示"睡眠"变体图标。
- 菜单：打开主窗口；每盘快捷切换（在线↔默认动作，休眠/离线→唤醒）；全部休眠(或全部离线)/全部唤醒；退出。
- 通知：定时触发结果、锁定失败+占用进程列表、磁盘插拔、运行时缺失引导。

## 5. 风险与验证

- Intel RST/RAID 模式下 pass-through 可能失败：枚举阶段试探，失败即标受限。
- STANDBY IMMEDIATE 不等待停转完成，UI 状态以"软件视角"为准；IDENTIFY 的 powermode 位可轮询确认（可选）。
- 解锁后卷重新挂载时机由系统决定，唤醒后短暂访问延迟属预期。
- 测试策略：Core/Native 用虚拟环境不可行（真 IOCTL），v1 以"真机手测清单"覆盖；DiskInventory 的分类逻辑与 Scheduler 时间计算做可注入的单元测试。
