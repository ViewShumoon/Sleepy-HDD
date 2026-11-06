# 用 IOCTL_ATA_PASS_THROUGH 发送 STANDBY IMMEDIATE 实现磁盘停转

要让固定 HDD 立即停转，唯一在 Win10/11 + storahci 上可靠可用的用户态途径是向 `\\.\PhysicalDriveN` 发送 ATA 命令序列：CACHE FLUSH EXT (0xE7) → STANDBY IMMEDIATE (0xE0)。被否决的替代方案：电源设置（`powercfg /disk-timeout`）只能设空闲超时，无公开 API 立即触发；`IOCTL_IDE_PASS_THROUGH`/`SMART_SEND_DRIVE_COMMAND` 旧路径在现代 AHCI 系统上已失效；`IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES` 仅用于 TRIM。必须同步调用；RAID/Intel RST 驱动下可能不可用，受管磁盘枚举时需检测并排除。

Considered options: 电源超时触发（不可立即）、旧 IDE pass-through（已死）、SCSI pass-through 包装 ATA_16（等价但更绕，SATA 盘直接走 ATA pass-through）。

Consequences: 唤醒不需要专用命令——ATA 规范规定 STANDBY 状态的盘收到任何 I/O 自动 spin-up；但绝不可使用 SLEEP (0xE6) 命令，它只能靠硬件复位唤醒，用户态无法恢复。
