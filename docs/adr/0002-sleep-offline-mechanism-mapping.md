# 休眠=卷锁定/卸载+停转，离线=磁盘属性离线+停转

三态模型（活跃/休眠/离线）到系统机制的映射：**休眠** = FSCTL_LOCK_VOLUME + FSCTL_DISMOUNT_VOLUME 全部卷，再发 STANDBY——盘符可见但访问被拒，I/O 到不了磁盘所以盘保持沉睡（保护模式，revoSleep 同款思路）；**离线** = 在休眠基础上追加 `IOCTL_DISK_SET_DISK_ATTRIBUTES` 的 `DISK_ATTRIBUTE_OFFLINE`——卷从系统完全消失。离线属性本身不停转也不防唤醒（枚举仍会吵醒磁盘），所以必须与 STANDBY 组合；反之单纯 STANDBY 不防访问（任何读都会自动 spin-up），所以必须与锁定组合。两个状态各由一个系统机制"撑住"，另一个机制负责停转。

Consequences: 含页面文件/系统卷的盘无法离线，动态盘/Storage Spaces 成员盘离线会导致池降级——这些盘在枚举阶段即标记为受限，不提供操作。卷锁不住（有打开的文件）时放弃操作并报告占用进程，不强杀句柄。
