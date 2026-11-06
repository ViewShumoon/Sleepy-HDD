# 整个应用以管理员权限运行，单进程承载 UI、托盘与磁盘引擎

ATA pass-through 要求对 `\\.\PhysicalDriveN` 的 GENERIC_READ|WRITE 句柄，卷锁定/卸载要求 SeManageVolumePrivilege，磁盘离线属性要求管理员——全部需要提权令牌。因此不做"普通权限 UI + 提权后台服务"的两进程架构，v1 用应用清单 `requireAdministrator` 整体提权，UI、托盘、引擎、定时器同进程。将来若出现"非提权场景常驻"的需求（如受限账户下查看状态）再拆服务。

Consequences: 开机自启不能走 Run 键（会在登录时弹 UAC），需用任务计划程序"最高权限运行"，见后续 ADR/设计。
