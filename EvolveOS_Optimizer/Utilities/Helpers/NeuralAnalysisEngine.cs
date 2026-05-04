// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class NeuralAnalysisEngine
    {
        public static string GenerateEventAnalysis(int eventId, string sourceName)
        {
            // 1. SERVICE ANOMALIES (7000 - 7499)
            if (eventId >= 7000 && eventId < 7500 && sourceName.StartsWith("ServiceMonitor|"))
            {
                string serviceName = sourceName.Split('|').Length > 1 ? sourceName.Split('|')[1] : "Unknown";
                return GenerateServiceAnomalyAnalysis(eventId, serviceName);
            }

            // 2. SECURITY ENGINE ANOMALIES (9101 - 9117)
            if (eventId >= 9101 && eventId <= 9117)
            {
                return ResourceString.GetString("diag_analysis_9101") ??
                       "⚠️ CRITICAL VULNERABILITY DETECTED.\n" +
                       "A core Windows security boundary (Defender, Firewall, or Kernel Isolation) is disabled or bypassed. " +
                       "Your system is actively exposed to zero-day exploits or unauthorized lateral movement. \n\n" +
                       "RECOMMENDATION: Open Windows Security immediately and restore the recommended defaults.";
            }

            // 3. SOFTWARE, KERNEL & SYSTEM BUCKETS
            return eventId switch
            {
                // 1 - 99: CORE KERNEL, STORAGE CONTROLLERS, & WUA

                1 => ResourceString.GetString("diag_analysis_1") ??
                    "System Wake Initialization. The system has resumed from a low-power sleep state. " +
                    "The kernel is actively re-enumerating the hardware bus and powering on connected peripherals. \n\n" +
                    "RECOMMENDATION: Routine power state telemetry. No action required.",
                2 or 3 => ResourceString.GetString("diag_analysis_2") ??
                    "File System Filter Manager Delay. A minifilter driver (usually third-party antivirus or backup software) took an excessive amount of time to inspect a file operation. " +
                    "This directly causes localized disk latency and Explorer UI stuttering. \n\n" +
                    "RECOMMENDATION: Temporarily disable real-time protection to isolate the latency source.",
                4 or 5 => ResourceString.GetString("diag_analysis_4") ??
                    "Storage Volume Mount Failure. The kernel volume manager failed to mount a logical partition. " +
                    "The partition table may be corrupted, or the drive lacks an assigned drive letter in Disk Management. \n\n" +
                    "RECOMMENDATION: Open Disk Management and verify partition health.",
                6 => ResourceString.GetString("diag_analysis_6") ??
                    "Filter Manager Unload. A file system filter driver was unloaded dynamically from active memory. \n\n" +
                    "RECOMMENDATION: Routine driver telemetry.",
                7 => ResourceString.GetString("diag_analysis_7") ??
                    "⚠️ PHYSICAL MEDIA FAULT. The storage controller detected a 'Bad Block' (dead sector) on the physical drive. " +
                    "This is a strong indicator of impending hardware failure on your SSD or HDD. \n\n" +
                    "RECOMMENDATION: Backup critical data immediately. Click 'Fix' to run a CHKDSK sweep to map out the dead sectors.",
                8 or 9 => ResourceString.GetString("diag_analysis_8") ??
                    "File System Filter Attach Failure. A backup or security filter driver failed to attach to a storage volume. \n\n" +
                    "RECOMMENDATION: Restart the associated background service.",
                10 => ResourceString.GetString("diag_analysis_10") ??
                    "Windows Update Agent (WUA) payload discovery error. The system could not properly parse the XML manifest from the Microsoft Update catalog. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to violently flush the Software Distribution download folders.",
                11 => ResourceString.GetString("diag_analysis_11") ??
                    "Disk Controller Timeout. The hard disk driver reported a severe hardware latency timeout. " +
                    "The drive is physically struggling to complete basic I/O read/write operations before the Windows kernel gives up. \n\n" +
                    "RECOMMENDATION: Verify SATA/NVMe cable seating and SMART health.",
                12 or 13 => ResourceString.GetString("diag_analysis_12") ??
                    "HAL / ACPI Firmware Warning. The Hardware Abstraction Layer received an invalid power state request from the motherboard firmware. " +
                    "The BIOS/UEFI provided an incomplete ACPI table to the Windows kernel. \n\n" +
                    "RECOMMENDATION: Update your motherboard BIOS/UEFI firmware.",
                14 => ResourceString.GetString("diag_analysis_14") ??
                    "Controller I/O Parity Error. The SATA or NVMe controller received corrupted data from the storage drive. " +
                    "The data was flipped during transit across the motherboard bus. \n\n" +
                    "RECOMMENDATION: Reseat the storage drive cables and check for motherboard flex.",
                15 => ResourceString.GetString("diag_analysis_15") ??
                    "Disk Device Not Ready. The storage miniport driver reported that the drive is not spinning or actively refuses to accept I/O commands. \n\n" +
                    "RECOMMENDATION: Ensure the power cable is securely connected to the drive.",
                16 => ResourceString.GetString("diag_analysis_16") ??
                    "Windows Search Service Failure. The indexing database (Windows.edb) is locked or has exceeded its maximum addressable size. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the Windows Search index.",
                17 => ResourceString.GetString("diag_analysis_17") ??
                    "Windows Update Network Error. The WUA client failed to establish a secure SSL connection to the Microsoft Update telemetry servers. \n\n" +
                    "RECOMMENDATION: Check local network connectivity and proxy settings.",
                18 => ResourceString.GetString("diag_analysis_18") ??
                    "WHEA Fatal Hardware Error. The Windows Hardware Error Architecture detected an uncorrectable hardware fault on the CPU or memory bus. \n\n" +
                    "RECOMMENDATION: The system likely generated a blue screen. Review minidump files immediately.",
                19 or 20 => ResourceString.GetString("diag_analysis_19") ??
                    "Windows Update installation failed to validate the downloaded payload package. " +
                    "The cryptographic hash does not match the official Microsoft manifest. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the SoftwareDistribution cache.",
                21 or 22 or 23 or 24 => ResourceString.GetString("diag_analysis_21") ??
                    "Windows Update Component Servicing. The CBS engine is actively staging update packages into the WinSxS folder. \n\n" +
                    "RECOMMENDATION: Routine servicing telemetry. Do not power off the machine.",
                25 => ResourceString.GetString("diag_analysis_25") ??
                    "Windows Update Failure. A specific payload update failed to install during the final commit phase. \n\n" +
                    "RECOMMENDATION: Review the Windows Update history to find the failing KB number.",
                26 or 27 or 28 => ResourceString.GetString("diag_analysis_26") ??
                    "File System Filter Manager configuration warning. A filter driver is operating in a legacy mode that degrades system performance. \n\n" +
                    "RECOMMENDATION: Update the associated software (Antivirus/Backup) to a modern architecture.",
                29 => ResourceString.GetString("diag_analysis_29") ??
                    "Windows Time Service (W32Time) Warning. The system clock is drastically desynchronized from the upstream NTP server. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a hard resync of the W32Time service.",
                30 or 31 or 32 => ResourceString.GetString("diag_analysis_30") ??
                    "Windows Update Agent failed to resolve a dependency chain. A required prerequisite update is missing from the local cache. \n\n" +
                    "RECOMMENDATION: Run the Windows Update Troubleshooter to fetch missing prerequisites.",
                33 => ResourceString.GetString("diag_analysis_33") ??
                    "Side-by-Side (SxS) Assembly Corruption. An application failed to start because its required C++ redistributable or manifest file is missing from the WinSxS store. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to run SFC/DISM and repair the component store.",
                34 => ResourceString.GetString("diag_analysis_34") ??
                    "Windows Update Setup Engine Failure. The update installation crashed during the crucial setup phase. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear the update cache and restart the WUAUSERV daemon.",
                35 => ResourceString.GetString("diag_analysis_35") ??
                    "System File Checker (SFC) found corrupted files and was unable to repair some of them. " +
                    "The local payload cache is too damaged to be used for recovery. \n\n" +
                    "RECOMMENDATION: Run a DISM online restore operation.",
                36 => ResourceString.GetString("diag_analysis_36") ??
                    "Time Synchronization Error. The W32Time service was unable to reach the configured NTP server due to a network timeout. \n\n" +
                    "RECOMMENDATION: Check your network connection and ensure port 123 is not blocked by a firewall.",
                37 or 38 => ResourceString.GetString("diag_analysis_37") ??
                    "Windows Update Component Cleanup. The OS is actively removing superseded update packages to free up disk space. \n\n" +
                    "RECOMMENDATION: Routine disk cleanup telemetry.",
                39 => ResourceString.GetString("diag_analysis_39") ??
                    "Volume Shadow Copy Service error. A VSS operation failed due to insufficient disk space on the target volume. \n\n" +
                    "RECOMMENDATION: Free up at least 15% of the total disk space on the drive.",
                40 => ResourceString.GetString("diag_analysis_40") ??
                    "Print Spooler configuration error. A legacy print driver failed to map its spooling directory. \n\n" +
                    "RECOMMENDATION: Reinstall the printer driver using a modern V4 package.",
                41 => ResourceString.GetString("diag_analysis_41") ??
                    "Kernel-Power Error (Code 41). The system rebooted without cleanly shutting down first. " +
                    "This is a severe hardware-level fault, typically caused by a failing Power Supply (PSU), extreme thermal throttling, or a sudden power loss. \n\n" +
                    "RECOMMENDATION: Verify physical power connections, monitor CPU/GPU temperatures, and check PSU stability.",
                42 => ResourceString.GetString("diag_analysis_42") ??
                    "Kernel Power Manager. The system is entering sleep or hibernation mode. \n\n" +
                    "RECOMMENDATION: Routine power transition.",
                43 or 44 or 45 or 46 => ResourceString.GetString("diag_analysis_43") ??
                    "Windows Update restart required. A downloaded package has been staged, but requires a system reboot to initialize the kernel hooks. \n\n" +
                    "RECOMMENDATION: Restart your PC to finalize the update.",
                47 => ResourceString.GetString("diag_analysis_47") ??
                    "Time Service Source fallback. The primary NTP server is unreachable, so Windows has shifted to the secondary CMOS hardware clock. \n\n" +
                    "RECOMMENDATION: Verify internet connectivity to time.windows.com.",
                48 or 49 => ResourceString.GetString("diag_analysis_48") ??
                    "System Time Change. The system time was modified by a user, an application, or the Windows Time Service. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                50 => ResourceString.GetString("diag_analysis_50") ??
                    "Delayed Write Failed. The OS could not commit data from the RAM cache to the physical disk. " +
                    "This indicates heavy disk saturation or an unexpected device removal. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush system buffers and discard stalled cache pages.",
                51 => ResourceString.GetString("diag_analysis_51") ??
                    "Paging operation error. A memory page failed to read from the paging file on the disk. " +
                    "This can cause severe application crashes or a complete system lockup. \n\n" +
                    "RECOMMENDATION: Verify your pagefile integrity and storage health.",
                52 => ResourceString.GetString("diag_analysis_52") ??
                    "NTFS Cache allocation warning. The system is struggling to map virtual memory to the physical disk platter. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the native cache.",
                53 => ResourceString.GetString("diag_analysis_53") ??
                    "Storage volume decryption failed. BitLocker or a third-party encryption tool could not unlock the drive metadata. \n\n" +
                    "RECOMMENDATION: Verify recovery keys and encryption status.",
                54 => ResourceString.GetString("diag_analysis_54") ??
                    "NTFS transaction rollback. A file operation was interrupted and safely reverted to protect data integrity. \n\n" +
                    "RECOMMENDATION: Minor I/O hiccup. No action required.",
                55 => ResourceString.GetString("diag_analysis_55") ??
                    "NTFS File System Corruption. The Master File Table (MFT) contains invalid record segments. " +
                    "The drive physically works, but the file structure is logically broken. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to schedule a deep-level CHKDSK /f offline repair.",
                56 or 57 or 58 => ResourceString.GetString("diag_analysis_56") ??
                    "NTFS Metadata synchronization error. The system could not flush the volume metadata to the physical disk platter. \n\n" +
                    "RECOMMENDATION: Check storage health and avoid abruptly unplugging drives.",
                59 or 60 => ResourceString.GetString("diag_analysis_59") ??
                    "DISM servicing operation failed. The image package could not be expanded due to a corrupted cab file. \n\n" +
                    "RECOMMENDATION: Run an online health restore via DISM /Online /Cleanup-Image /RestoreHealth.",
                (>= 61 and <= 68) => ResourceString.GetString("diag_analysis_61") ??
                    "Generic system resource exhaustion or minor background service transition delay. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.",
                69 => ResourceString.GetString("diag_analysis_69") ??
                    "AppX Deployment Failure. A UWP application failed to activate or deploy due to a broken package manifest. \n\n" +
                    "RECOMMENDATION: Re-register the specific Windows Store application.",
                (>= 70 and <= 97) => ResourceString.GetString("diag_analysis_70") ??
                    "Minor kernel PNP, driver transition, or telemetry logging event. \n\n" +
                    "RECOMMENDATION: Routine telemetry. No action required.",
                98 => ResourceString.GetString("diag_analysis_98") ??
                    "NTFS Volume requires an online scan. Minor logical inconsistencies were detected. \n\n" +
                    "RECOMMENDATION: Run an online disk optimization.",
                99 => ResourceString.GetString("diag_analysis_99") ??
                    "System Boot Manager anomaly. The boot configuration data (BCD) was modified or evaluated incorrectly. \n\n" +
                    "RECOMMENDATION: Run bcdedit to verify boot configuration.",

                // 100 - 499: TASK SCHEDULER, VDS, ESENT, & APPX

                (>= 100 and <= 108) => ResourceString.GetString("diag_analysis_100") ??
                    "Task Scheduler Fault. A registered background task failed to launch. " +
                    "The specified executable path is missing, or the service account lacks the required execution privileges. \n\n" +
                    "RECOMMENDATION: Verify the integrity of custom tasks in the Task Scheduler Library.",
                109 => ResourceString.GetString("diag_analysis_109") ??
                    "Kernel Memory Manager Fault. The OS encountered a non-paged pool exhaustion. " +
                    "A critical driver has leaked memory and consumed all protected kernel RAM. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a native memory cleanup and flush the standby list.",
                110 => ResourceString.GetString("diag_analysis_110") ??
                    "Invalid memory mapping attempt detected by the Kernel Memory Manager. A driver attempted to access a protected memory sector. \n\n" +
                    "RECOMMENDATION: Check for aggressive overclocking or failing RAM modules.",
                (>= 111 and <= 116) => ResourceString.GetString("diag_analysis_111") ??
                    "Task Scheduler triggered an automated maintenance task (e.g., defragmentation, telemetry upload). \n\n" +
                    "RECOMMENDATION: Routine maintenance telemetry.",
                117 => ResourceString.GetString("diag_analysis_117") ??
                    "Memory diagnostic results reported. The Windows Memory Diagnostic tool finished scanning physical RAM. \n\n" +
                    "RECOMMENDATION: Review the Event Viewer for specific memory hardware faults.",
                (>= 118 and <= 122) => ResourceString.GetString("diag_analysis_118") ??
                    "Task Scheduler queue delay. The system has too many concurrent tasks running, causing background job latency. \n\n" +
                    "RECOMMENDATION: Review and disable unnecessary scheduled tasks.",
                123 => ResourceString.GetString("diag_analysis_123") ??
                    "Print Spooler job termination. A document failed to print and was discarded from the active queue. \n\n" +
                    "RECOMMENDATION: Check printer connectivity and restart the spooler.",
                (>= 124 and <= 128) => ResourceString.GetString("diag_analysis_124") ??
                    "Task Scheduler completed a routine background task successfully. \n\n" +
                    "RECOMMENDATION: Routine automation telemetry.",
                129 => ResourceString.GetString("diag_analysis_129") ??
                    "Storage miniport driver timeout. The disk did not respond to an access request within the expected threshold. " +
                    "This usually causes brief UI freezes as the OS waits for the disk to wake up. \n\n" +
                    "RECOMMENDATION: Check Windows Power settings and prevent the hard disk from sleeping.",
                130 => ResourceString.GetString("diag_analysis_130") ??
                    "NTFS volume bitmap is damaged or a file system transaction could not be rolled back. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to schedule an offline volume repair.",
                131 => ResourceString.GetString("diag_analysis_131") ??
                    "Windows Time Service established a secure connection with a configured NTP server. \n\n" +
                    "RECOMMENDATION: Time synchronization is healthy.",
                (>= 132 and <= 136) => ResourceString.GetString("diag_analysis_132") ??
                    "Virtual Disk Service (VDS) Anomaly. A dynamic volume allocation or VHDX mounting operation failed or timed out. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the VDS provider and rescan the partition layout.",
                137 => ResourceString.GetString("diag_analysis_137") ??
                    "The system firmware has changed the processor's memory type range registers (MTRRs) across a sleep state transition. \n\n" +
                    "RECOMMENDATION: This is a BIOS handling error. Resume from sleep may be degraded.",
                138 or 139 => ResourceString.GetString("diag_analysis_138") ??
                    "VDS Provider generated an alert regarding dynamic disk synchronization. \n\n" +
                    "RECOMMENDATION: Verify dynamic volume health in Disk Management.",
                140 => ResourceString.GetString("diag_analysis_140") ??
                    "NTFS Transaction log is full or corrupted. The file system cannot write any more rollback data. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the disk cache and verify file system integrity.",
                141 or 142 or 143 => ResourceString.GetString("diag_analysis_141") ??
                    "Storage hardware error. A physical disk drive reported an internal error via SMART diagnostics. \n\n" +
                    "RECOMMENDATION: Immediately backup critical data. The drive may fail soon.",
                144 => ResourceString.GetString("diag_analysis_144") ??
                    "Time Service synchronized successfully using a fallback hardware clock. \n\n" +
                    "RECOMMENDATION: Routine time telemetry.",
                (>= 145 and <= 152) => ResourceString.GetString("diag_analysis_145") ??
                    "Storage volume manager reported a minor allocation or partition size adjustment. \n\n" +
                    "RECOMMENDATION: Routine disk management event.",
                153 => ResourceString.GetString("diag_analysis_153") ??
                    "Storage Controller Reset. The SATA/NVMe controller had to be reset because a disk I/O operation took too long to complete. " +
                    "This causes massive system freezes (10-30 seconds) and UI lockups. \n\n" +
                    "RECOMMENDATION: Update your motherboard storage controller drivers and verify firmware health.",
                154 => ResourceString.GetString("diag_analysis_154") ??
                    "Storage IO control failure. A direct hardware command was rejected by the drive firmware. \n\n" +
                    "RECOMMENDATION: Update SSD firmware.",
                (>= 155 and <= 164) => ResourceString.GetString("diag_analysis_155") ??
                    "Storage Spaces or RAID logical partition fault. A logical drive in a mirrored or parity space has degraded, resynced, or dropped offline. \n\n" +
                    "RECOMMENDATION: Review Windows Storage Spaces health immediately.",
                (>= 165 and <= 199) => ResourceString.GetString("diag_analysis_165") ??
                    "System Setup or out-of-box experience (OOBE) background tracking. \n\n" +
                    "RECOMMENDATION: Routine OS installation telemetry.",
                (>= 200 and <= 202) => ResourceString.GetString("diag_analysis_200") ??
                    "Component Based Servicing (CBS) installation initiated or completed for a system package. \n\n" +
                    "RECOMMENDATION: Routine update tracking.",
                (>= 203 and <= 299) => ResourceString.GetString("diag_analysis_203") ??
                    "Device Setup tracking and PNP enumerations during boot. \n\n" +
                    "RECOMMENDATION: Routine boot telemetry.",
                (>= 300 and <= 308) => ResourceString.GetString("diag_analysis_300") ??
                    "Component Based Servicing (CBS) manifest corruption or missing SxS assembly payload. " +
                    "A critical system package is physically missing from the WinSxS store. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to trigger an online DISM cleanup and restoration process.",
                309 => ResourceString.GetString("diag_analysis_309") ??
                    "Print Spooler failed to load a third-party print provider DLL. \n\n" +
                    "RECOMMENDATION: Reinstall the printer software.",
                310 => ResourceString.GetString("diag_analysis_310") ??
                    "Print Spooler port monitor error. The system cannot communicate with the physical printer endpoint. \n\n" +
                    "RECOMMENDATION: Verify printer network IP or USB connection.",
                (>= 311 and <= 314) => ResourceString.GetString("diag_analysis_311") ??
                    "Print Spooler rendering delay. The document is taking too long to convert to EMF spool format. \n\n" +
                    "RECOMMENDATION: Restart the spooler service if the queue is stuck.",
                315 or 316 => ResourceString.GetString("diag_analysis_315") ??
                    "The Print Spooler subsystem crashed or encountered a severe job corruption. " +
                    "This usually happens when rendering a complex document or loading a misconfigured 3rd-party print driver module. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to purge orphaned print jobs and restart the Spooler pipeline.",
                317 => ResourceString.GetString("diag_analysis_317") ??
                    "WMI Service boot-start timeout. The Windows Management Instrumentation repository took too long to respond to the host process. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to run a salvaging operation on the WMI repository.",
                (>= 318 and <= 399) => ResourceString.GetString("diag_analysis_318") ??
                    "Generic system resource transition state logging. \n\n" +
                    "RECOMMENDATION: Routine OS telemetry.",
                (>= 400 and <= 411) => ResourceString.GetString("diag_analysis_400") ??
                    "Component Based Servicing (CBS) error. A package failed to uncompress or stage into the deployment image. \n\n" +
                    "RECOMMENDATION: Clear the Software Distribution folder.",
                (>= 412 and <= 426) => ResourceString.GetString("diag_analysis_412") ??
                    "CBS / Deployment Image Servicing and Management (DISM) background processing logs. \n\n" +
                    "RECOMMENDATION: Routine servicing telemetry.",
                427 => ResourceString.GetString("diag_analysis_427") ??
                    "Extensible Storage Engine (ESENT) database engine started successfully. \n\n" +
                    "RECOMMENDATION: Routine database telemetry.",
                (>= 428 and <= 440) => ResourceString.GetString("diag_analysis_428") ??
                    "ESENT transaction or checkpointing background logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                441 or 442 => ResourceString.GetString("diag_analysis_441") ??
                    "Extensible Storage Engine (ESENT) encountered a minor data inconsistency or version mismatch in a local database. \n\n" +
                    "RECOMMENDATION: The database will attempt self-recovery.",
                (>= 443 and <= 446) => ResourceString.GetString("diag_analysis_443") ??
                    "ESENT log file cleanup or background defragmentation. \n\n" +
                    "RECOMMENDATION: Routine optimization telemetry.",
                447 or 448 => ResourceString.GetString("diag_analysis_447") ??
                    "ESENT cache allocation or memory limit warning. The database engine is struggling for RAM. \n\n" +
                    "RECOMMENDATION: Close background apps to free up memory.",
                (>= 449 and <= 450) => ResourceString.GetString("diag_analysis_449") ??
                    "ESENT database attached and recovered from an unclean shutdown state. \n\n" +
                    "RECOMMENDATION: Self-healing successful.",
                451 => ResourceString.GetString("diag_analysis_451") ??
                    "ESENT database background task timeout. A query took too long to execute. \n\n" +
                    "RECOMMENDATION: Check disk health.",
                (>= 452 and <= 453) => ResourceString.GetString("diag_analysis_452") ??
                    "ESENT secondary index generation or restructuring logs. \n\n" +
                    "RECOMMENDATION: Routine database maintenance.",
                454 => ResourceString.GetString("diag_analysis_454") ??
                    "ESENT Database Recovery Failure. The database engine could not restore a database (often Windows Search or TileDataLayer) from its log files. \n\n" +
                    "RECOMMENDATION: The database may need to be deleted and rebuilt.",
                455 => ResourceString.GetString("diag_analysis_455") ??
                    "Extensible Storage Engine (ESENT) database corruption detected. " +
                    "This directly impacts the TileDataLayer, Windows Search, and the App Repository. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the local database directory structures.",
                (>= 456 and <= 466) => ResourceString.GetString("diag_analysis_456") ??
                    "ESENT minor operational telemetry and cache tuning logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                467 => ResourceString.GetString("diag_analysis_467") ??
                    "ESENT Index Corruption. A database index is fundamentally broken and must be rebuilt. \n\n" +
                    "RECOMMENDATION: Rebuild the Windows Search index.",
                (>= 468 and <= 473) => ResourceString.GetString("diag_analysis_468") ??
                    "ESENT background thread transitions. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                474 => ResourceString.GetString("diag_analysis_474") ??
                    "ESENT page read failure. A specific page of the database could not be loaded from the disk. \n\n" +
                    "RECOMMENDATION: Run CHKDSK to verify disk integrity.",
                475 or 476 => ResourceString.GetString("diag_analysis_475") ??
                    "ESENT log file sequence mismatch. The transaction logs do not match the database state. \n\n" +
                    "RECOMMENDATION: The database will likely trigger a full repair.",
                477 => ResourceString.GetString("diag_analysis_477") ??
                    "ESENT critical I/O failure. The storage subsystem dropped a read/write request. \n\n" +
                    "RECOMMENDATION: Investigate storage hardware health.",
                (>= 478 and <= 480) => ResourceString.GetString("diag_analysis_478") ??
                    "ESENT database detachment and shutdown logs. \n\n" +
                    "RECOMMENDATION: Routine shutdown telemetry.",
                481 or 482 => ResourceString.GetString("diag_analysis_481") ??
                    "ESENT log file disk space exhaustion. The database cannot expand because the drive is completely full. \n\n" +
                    "RECOMMENDATION: Free up disk space immediately.",
                483 => ResourceString.GetString("diag_analysis_483") ??
                    "Print Spooler failure. The spooler cannot allocate enough memory to process a massive print job. \n\n" +
                    "RECOMMENDATION: Cancel the print job and split the document.",
                (>= 484 and <= 487) => ResourceString.GetString("diag_analysis_484") ??
                    "Generic system resource threshold warnings. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.",
                488 => ResourceString.GetString("diag_analysis_488") ??
                    "ESENT transaction log size threshold exceeded. \n\n" +
                    "RECOMMENDATION: The system will automatically truncate logs.",
                489 or 490 => ResourceString.GetString("diag_analysis_489") ??
                    "Start Menu or Shell Experience host dropped a rendering frame or encountered an ESENT read block. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to safely restart the Explorer process.",
                491 or 492 or 493 => ResourceString.GetString("diag_analysis_491") ??
                    "AppX Manifest Rendering Drop. The shell experienced an exception while trying to draw an AppX Live Tile. \n\n" +
                    "RECOMMENDATION: Clear the local AppData tile cache.",
                (>= 494 and <= 499) => ResourceString.GetString("diag_analysis_494") ??
                    "AppX or Shell Host minor background telemetry tracking. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",

                // 500 - 999: WHEA, FAST STARTUP, & DIAGNOSTICS

                (>= 500 and <= 503) => ResourceString.GetString("diag_analysis_500") ??
                    "Windows Hardware Error Architecture (WHEA) corrected machine check exception. " +
                    "The CPU or RAM caught and fixed a transient hardware error (ECC) before it could crash the system. \n\n" +
                    "RECOMMENDATION: Monitor hardware stability. If frequent, hardware replacement (CPU/RAM) may be necessary.",
                504 or 505 => ResourceString.GetString("diag_analysis_504") ??
                    "WHEA uncorrectable error telemetry. The hardware encountered a fault it could not fix, resulting in a system halt. \n\n" +
                    "RECOMMENDATION: Review minidump files. Hardware may be critically failing.",
                506 or 507 => ResourceString.GetString("diag_analysis_506") ??
                    "Fast Startup failure. The system failed to load the hibernation file during boot, falling back to a cold boot. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to cleanly rebuild the hibernation cache.",
                508 or 509 => ResourceString.GetString("diag_analysis_508") ??
                    "WHEA cache hierarchy error. The L1/L2/L3 CPU cache encountered a parity mismatch. \n\n" +
                    "RECOMMENDATION: Remove aggressive CPU overclocks or undervolts.",
                510 => ResourceString.GetString("diag_analysis_510") ??
                    "AppX Deployment Warning. A UWP app package has an invalid digital signature. \n\n" +
                    "RECOMMENDATION: Reinstall the app from the Windows Store.",
                511 or 512 => ResourceString.GetString("diag_analysis_511") ??
                    "WHEA PCIe bus fault. A device attached to the PCI Express bus generated a hardware-level error. \n\n" +
                    "RECOMMENDATION: Reseat GPU or NVMe drives.",
                513 => ResourceString.GetString("diag_analysis_513") ??
                    "Cryptographic Services (Catroot2) corruption. Windows cannot verify system file signatures securely, preventing Windows Update from running. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to stop CryptSvc, purge the Catroot2 cache, and restart the daemon.",
                514 or 515 => ResourceString.GetString("diag_analysis_514") ??
                    "AppX Licensing or Provisioning error. An app cannot launch because its Store license could not be validated. \n\n" +
                    "RECOMMENDATION: Run WSReset to clear the Store cache.",
                (>= 516 and <= 523) => ResourceString.GetString("diag_analysis_516") ??
                    "AppX Deployment subsystem background tracing and minor manifest parsing anomalies. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to globally re-register all AppX manifests.",
                524 or 525 => ResourceString.GetString("diag_analysis_524") ??
                    "Active power policy timeout. A background service or device prevented the system from entering a low-power state. \n\n" +
                    "RECOMMENDATION: Run 'powercfg /requests' to find the blocking service.",
                (>= 526 and <= 532) => ResourceString.GetString("diag_analysis_526") ??
                    "WHEA diagnostic payload. A specific hardware component reported a non-fatal hardware error event to the kernel. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear the WER diagnostic queues.",
                533 => ResourceString.GetString("diag_analysis_533") ??
                    "Active power policy modification failed or timed out. Windows could not apply the requested power plan. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset power plans to default.",
                (>= 534 and <= 565) => ResourceString.GetString("diag_analysis_534") ??
                    "WHEA / WER generic diagnostic payload generation. The system is packaging hardware fault data to send to Microsoft telemetry servers. \n\n" +
                    "RECOMMENDATION: Routine diagnostic upload. Monitor for associated BSODs.",
                566 => ResourceString.GetString("diag_analysis_566") ??
                    "Power management notification timeout. A driver took too long to acknowledge a sleep state transition. \n\n" +
                    "RECOMMENDATION: Update specific hardware drivers.",
                (>= 567 and <= 807) => ResourceString.GetString("diag_analysis_567") ??
                    "Generic low-level kernel and service state transition telemetry. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.",
                808 => ResourceString.GetString("diag_analysis_808") ??
                    "Print Spooler Document Load Error. The spooler could not render the document metadata. \n\n" +
                    "RECOMMENDATION: Clear the print queue and try again.",
                (>= 809 and <= 999) => ResourceString.GetString("diag_analysis_809") ??
                    "Generic subsystem state transition and diagnostic logging. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",

                // 1000 - 1999: APP CRASHES, DNS, DCOM, & PROFILES

                1000 => ResourceString.GetString("diag_analysis_1000") ??
                    "Application Crash (Exception Fault). A user-mode program terminated unexpectedly due to an unhandled memory access violation or missing DLL module. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to run SFC/DISM to ensure all native Windows dependencies are intact.",
                1001 => ResourceString.GetString("diag_analysis_1001") ??
                    "Windows Error Reporting (WER) generated a crash dump for a failing application. \n\n" +
                    "RECOMMENDATION: Check the Windows Reliability Monitor for the specific application name.",
                1002 => ResourceString.GetString("diag_analysis_1002") ??
                    "Application Hang (Deadlock). A program stopped communicating with the Windows Desktop Window Manager. " +
                    "The process is deadlocked waiting for a resource, network response, or thread lock. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully clear the UI memory and restart the Explorer shell.",
                1003 => ResourceString.GetString("diag_analysis_1003") ??
                    "Application Dependency Error. A required background service or COM component failed to start, causing a primary application to terminate. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to trace and repair broken dependency hierarchies.",
                1004 => ResourceString.GetString("diag_analysis_1004") ??
                    "Bad Module Block. A loaded Dynamic Link Library (DLL) was corrupted in active memory. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to run a native cache purge.",
                1005 => ResourceString.GetString("diag_analysis_1005") ??
                    "Privilege Block. An application requested a restricted OS privilege and was blocked by the security descriptor. \n\n" +
                    "RECOMMENDATION: Launch the target application as an Administrator.",
                1006 => ResourceString.GetString("diag_analysis_1006") ??
                    "Buffer Overflow Exception. The application triggered a memory overflow. This is often a sign of poor software coding or a targeted exploit attempt. \n\n" +
                    "RECOMMENDATION: Ensure the application is updated to its latest version.",
                1007 => ResourceString.GetString("diag_analysis_1007") ??
                    "Stack Exhaustion. A stack overflow event was detected within a process thread. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear orphaned execution threads.",
                1008 => ResourceString.GetString("diag_analysis_1008") ??
                    "Heap Corruption. A heap corruption was detected. The application overwrote memory boundaries. \n\n" +
                    "RECOMMENDATION: Restart the application. If this persists, the executable is damaged.",
                1009 => ResourceString.GetString("diag_analysis_1009") ??
                    "Application Error. The OS aborted the execution of a process due to a corrupted instruction pointer. \n\n" +
                    "RECOMMENDATION: Run a malware scan to ensure the binary hasn't been hijacked.",
                1010 => ResourceString.GetString("diag_analysis_1010") ??
                    "WerFault background telemetry execution. The system generated a crash dump and uploaded it to Microsoft. \n\n" +
                    "RECOMMENDATION: Routine diagnostic tracking.",
                1011 => ResourceString.GetString("diag_analysis_1011") ??
                    "Power supply or battery subsystem generated an exception during a state polling event. \n\n" +
                    "RECOMMENDATION: Ensure laptop batteries are functioning properly.",
                1012 => ResourceString.GetString("diag_analysis_1012") ??
                    "DNS Client cache corruption. The local DNS cache contains invalid or malicious routing data. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the DNS cache.",
                1013 => ResourceString.GetString("diag_analysis_1013") ??
                    "DNS Client hosts file parse error. The system's local HOSTS file contains malformed syntax. \n\n" +
                    "RECOMMENDATION: Review the C:\\Windows\\System32\\drivers\\etc\\hosts file.",
                1014 => ResourceString.GetString("diag_analysis_1014") ??
                    "DNS Resolution Timeout. The DNS Client service failed to translate a domain name into an IP address. " +
                    "Your internet connection is active, but your DNS provider is not responding. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the DNS resolver cache.",
                1015 or 1016 => ResourceString.GetString("diag_analysis_1015") ??
                    "DNS Client encountered a socket error or caching limit constraint. \n\n" +
                    "RECOMMENDATION: Restart the DNS Client service.",
                1017 or 1018 or 1019 => ResourceString.GetString("diag_analysis_1017") ??
                    "DNS Client background network polling events and interface transition states. \n\n" +
                    "RECOMMENDATION: Routine network telemetry.",
                1020 or 1021 => ResourceString.GetString("diag_analysis_1020") ??
                    "MSI Installer / MsiExec background thread hung during a software modification state. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the MSI server registration.",
                1022 => ResourceString.GetString("diag_analysis_1022") ??
                    "Explorer Shell Exception. The primary Windows desktop process encountered a fatal error and restarted automatically. \n\n" +
                    "RECOMMENDATION: If frequent, disable conflicting shell extensions.",
                1023 => ResourceString.GetString("diag_analysis_1023") ??
                    ".NET Runtime Environment error. A background process failed to load the required .NET assembly. \n\n" +
                    "RECOMMENDATION: Repair your .NET Framework installation via Windows Features.",
                1024 or 1025 => ResourceString.GetString("diag_analysis_1024") ??
                    "MSI Installer engine logged a successful uninstallation or repair transaction. \n\n" +
                    "RECOMMENDATION: Routine software management tracking.",
                1026 => ResourceString.GetString("diag_analysis_1026") ??
                    ".NET Runtime Exception. A background application built on the .NET Framework crashed due to an unhandled exception. \n\n" +
                    "RECOMMENDATION: Ensure all .NET Framework redistributables are fully updated.",
                (>= 1027 and <= 1029) => ResourceString.GetString("diag_analysis_1027") ??
                    "Generic .NET or Application Host telemetry logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                1030 => ResourceString.GetString("diag_analysis_1030") ??
                    "Group Policy Error. The system failed to query the Domain Controller for updated GPO configurations. \n\n" +
                    "RECOMMENDATION: Check network connectivity to the domain.",
                1031 or 1032 => ResourceString.GetString("diag_analysis_1031") ??
                    "Application popup or service notification warning. \n\n" +
                    "RECOMMENDATION: Check desktop for active modal dialogs.",
                1033 => ResourceString.GetString("diag_analysis_1033") ??
                    "MSI Installer detected that an application was successfully installed. \n\n" +
                    "RECOMMENDATION: Routine software management tracking.",
                (>= 1034 and <= 1052) => ResourceString.GetString("diag_analysis_1034") ??
                    "Generic group policy, MSI, and network transition state logging. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                1053 or 1054 or 1055 => ResourceString.GetString("diag_analysis_1053") ??
                    "Group Policy Sync Failure. The Kerberos ticket has expired, or the LDAP connection to the controller dropped. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a hard GPUpdate /force.",
                1056 or 1057 => ResourceString.GetString("diag_analysis_1056") ??
                    "Group policy background refresh completed successfully. \n\n" +
                    "RECOMMENDATION: Routine active directory telemetry.",
                1058 => ResourceString.GetString("diag_analysis_1058") ??
                    "Group Policy file access error. The system cannot read the GPT.ini file from the SYSVOL share. \n\n" +
                    "RECOMMENDATION: Verify file share permissions on the domain controller.",
                (>= 1059 and <= 1073) => ResourceString.GetString("diag_analysis_1059") ??
                    "Generic print, terminal services, and background shell reporting logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                1074 => ResourceString.GetString("diag_analysis_1074") ??
                    "Clean Shutdown initiated. A user or an application successfully requested a graceful system reboot. \n\n" +
                    "RECOMMENDATION: No action required. The OS state is healthy.",
                1075 => ResourceString.GetString("diag_analysis_1075") ??
                    "System shutdown process is actively terminating user-mode applications. \n\n" +
                    "RECOMMENDATION: Routine shutdown transition.",
                1076 => ResourceString.GetString("diag_analysis_1076") ??
                    "Dirty Shutdown Tracking. A user provided a reason for the previous unexpected shutdown via the Shutdown Event Tracker. \n\n" +
                    "RECOMMENDATION: System reliability monitoring updated.",
                (>= 1077 and <= 1095) => ResourceString.GetString("diag_analysis_1077") ??
                    "Generic shutdown transition and service suspension telemetry. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.",
                1096 => ResourceString.GetString("diag_analysis_1096") ??
                    "Group Policy Registry extension error. A policy could not be applied to the local machine registry. \n\n" +
                    "RECOMMENDATION: Check for registry permission locks.",
                (>= 1097 and <= 1100) => ResourceString.GetString("diag_analysis_1097") ??
                    "Generic event log parsing or formatting warnings. \n\n" +
                    "RECOMMENDATION: Minor telemetry parsing error.",
                1101 => ResourceString.GetString("diag_analysis_1101") ??
                    "Audit Log Cleared. The Windows Security or System event log was manually cleared by an administrator. " +
                    "In an enterprise environment, this is a strong indicator of an attacker covering their tracks. \n\n" +
                    "RECOMMENDATION: Verify that this action was authorized by IT security.",
                1102 => ResourceString.GetString("diag_analysis_1102") ??
                    "Audit Log Cleared (Security Subsystem). The core security audit log was dumped. \n\n" +
                    "RECOMMENDATION: High severity if unprompted. Investigate for system compromise.",
                1103 => ResourceString.GetString("diag_analysis_1103") ??
                    "Event Log service initialized successfully. \n\n" +
                    "RECOMMENDATION: Routine service startup.",
                1104 or 1105 => ResourceString.GetString("diag_analysis_1104") ??
                    "Security event log reached its maximum capacity. Older events are being overwritten or archived. \n\n" +
                    "RECOMMENDATION: Increase maximum log size in Event Viewer if longer retention is needed.",
                1106 or 1107 => ResourceString.GetString("diag_analysis_1106") ??
                    "Generic event log archiving process initialized. \n\n" +
                    "RECOMMENDATION: Routine logging maintenance.",
                1108 => ResourceString.GetString("diag_analysis_1108") ??
                    "Event logging service encountered an error while processing an incoming security event. \n\n" +
                    "RECOMMENDATION: The system may be under extremely heavy I/O load.",
                (>= 1109 and <= 1115) => ResourceString.GetString("diag_analysis_1109") ??
                    "Generic diagnostic framework state transitions. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                (>= 1116 and <= 1119) => ResourceString.GetString("diag_analysis_1116") ??
                    "Windows Defender Engine encountered a definition update timeout or real-time protection crash. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a direct signature payload update via PowerShell.",
                (>= 1120 and <= 1499) => ResourceString.GetString("diag_analysis_1120") ??
                    "Broad range of background service, network mapping, and task scheduler generic logs. \n\n" +
                    "RECOMMENDATION: Routine OS telemetry. No action required.",
                1500 => ResourceString.GetString("diag_analysis_1500") ??
                    "User Profile Service: You have been logged on with a temporary profile. Changes made to this profile will be lost when you log off. \n\n" +
                    "RECOMMENDATION: Restart the computer. If the issue persists, the NTUSER.DAT file is corrupted.",
                1501 => ResourceString.GetString("diag_analysis_1501") ??
                    "User Profile Service: Your roaming profile was successfully loaded. \n\n" +
                    "RECOMMENDATION: Routine logon telemetry.",
                1502 => ResourceString.GetString("diag_analysis_1502") ??
                    "User Profile Service: The local profile was loaded successfully, but the roaming network profile is inaccessible. \n\n" +
                    "RECOMMENDATION: You are operating on cached credentials. Check network status.",
                1503 => ResourceString.GetString("diag_analysis_1503") ??
                    "User Profile Service: The roaming profile has successfully synchronized back to the server. \n\n" +
                    "RECOMMENDATION: Routine logoff telemetry.",
                1504 => ResourceString.GetString("diag_analysis_1504") ??
                    "User Profile Service: The roaming profile could not be synchronized with the remote server due to a network or permission error. \n\n" +
                    "RECOMMENDATION: Verify network connectivity to the domain controller.",
                (>= 1505 and <= 1507) => ResourceString.GetString("diag_analysis_1505") ??
                    "User Profile Service: Minor synchronization delays or file copy retries during profile loading. \n\n" +
                    "RECOMMENDATION: Monitor logon times.",
                1508 => ResourceString.GetString("diag_analysis_1508") ??
                    "User Profile Service: The registry could not load the user hive (NTUSER.DAT). The file may be locked by another process or antivirus. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a handle release protocol.",
                1509 => ResourceString.GetString("diag_analysis_1509") ??
                    "User Profile Service: A specific file is in use and cannot be copied to the roaming profile directory. \n\n" +
                    "RECOMMENDATION: Close all running applications before logging off.",
                1510 => ResourceString.GetString("diag_analysis_1510") ??
                    "User Profile Service: The user profile has been successfully unloaded. \n\n" +
                    "RECOMMENDATION: Routine logoff telemetry.",
                1511 => ResourceString.GetString("diag_analysis_1511") ??
                    "User Profile Service: The local profile is corrupted. Windows is creating a backup and generating a fresh profile. \n\n" +
                    "RECOMMENDATION: Monitor user data for potential file loss.",
                (>= 1512 and <= 1514) => ResourceString.GetString("diag_analysis_1512") ??
                    "User Profile Service: Profile restoration or backup state telemetry. \n\n" +
                    "RECOMMENDATION: The system is self-healing the profile.",
                1515 => ResourceString.GetString("diag_analysis_1515") ??
                    "User Profile Service: The user profile has been backed up successfully after a detected corruption event. \n\n" +
                    "RECOMMENDATION: No immediate action required.",
                1516 => ResourceString.GetString("diag_analysis_1516") ??
                    "User Profile Service: Profile registry keys were successfully updated. \n\n" +
                    "RECOMMENDATION: Routine profile management.",
                1517 => ResourceString.GetString("diag_analysis_1517") ??
                    "User Profile Service: The registry leaked a handle during logoff. A background app failed to release the NTUSER.DAT hive. \n\n" +
                    "RECOMMENDATION: Ensure all applications are cleanly closed before shutting down the PC.",
                (>= 1518 and <= 1520) => ResourceString.GetString("diag_analysis_1518") ??
                    "User Profile Service: Generic file copy and synchronization tracking. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                1521 => ResourceString.GetString("diag_analysis_1521") ??
                    "User Profile Service: The system cannot find the file specified for the user profile. The path in the ProfileList registry key is broken. \n\n" +
                    "RECOMMENDATION: Rebuild the local user profile via Advanced System Settings.",
                (>= 1522 and <= 1529) => ResourceString.GetString("diag_analysis_1522") ??
                    "User Profile Service: Minor directory access warnings or permission resets. \n\n" +
                    "RECOMMENDATION: Routine profile adjustments.",
                1530 => ResourceString.GetString("diag_analysis_1530") ??
                    "User Profile Service: Windows detected your registry file is still in use by other applications or services. The profile will be forcibly unloaded. \n\n" +
                    "RECOMMENDATION: This is typical in modern Windows and generally benign.",
                1531 or 1532 => ResourceString.GetString("diag_analysis_1531") ??
                    "User Profile Service: Generic state transition or registry handle leak detected during the logoff sequence. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to release locked registry handles.",
                1533 => ResourceString.GetString("diag_analysis_1533") ??
                    "System Resource Manager generic warning. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                1534 => ResourceString.GetString("diag_analysis_1534") ??
                    "Distributed COM (DCOM) Profile Extension Error. A COM server attempted to load into a user profile that is already unloading. \n\n" +
                    "RECOMMENDATION: Benign background race condition. No action required.",
                (>= 1535 and <= 1541) => ResourceString.GetString("diag_analysis_1535") ??
                    "Generic DCOM or user profile permission tracking. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                1542 => ResourceString.GetString("diag_analysis_1542") ??
                    "User Profile Service: The registry hive (classes) cannot be loaded. The user's AppX and file associations may be broken. \n\n" +
                    "RECOMMENDATION: Restart the machine to clear registry locks.",
                (>= 1543 and <= 1800) => ResourceString.GetString("diag_analysis_1543") ??
                    "Broad range of background service, network mapping, and task scheduler generic logs. \n\n" +
                    "RECOMMENDATION: Routine OS telemetry. No action required.",
                1801 => ResourceString.GetString("diag_analysis_1801") ??
                    "Advanced cryptographic staging for Secure Boot Key enrollment has been triggered by the OS. " +
                    "Your system requires a hardware-level handshake to update its trusted certificates. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to write the updated DB/DBX signatures to your motherboard firmware.",
                (>= 1802 and <= 1999) => ResourceString.GetString("diag_analysis_1802") ??
                    "Generic crypto, network, and setup telemetry logs. \n\n" +
                    "RECOMMENDATION: Routine OS overhead tracking.",

                // 2000 - 3999: DWM, CBS, NPS, & SEARCH

                2000 => ResourceString.GetString("diag_analysis_2000") ??
                    "Desktop Window Manager (DWM) composition warning. The system is experiencing a GDI Handle Leak or GPU context loss. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the shell and clear DWM memory.",
                2001 or 2002 or 2003 or 2004 or 2005 => ResourceString.GetString("diag_ai_dwm_exhaustion") ??
                    "DWM video memory allocation failed. The GPU has run out of VRAM to draw the desktop windows.\n\n" +
                    "RECOMMENDATION: Close heavy graphics applications or click 'Fix' to restart the DWM process.",
                (>= 2006 and <= 2009) => ResourceString.GetString("diag_analysis_2006") ??
                    "Generic desktop composition and memory tuning telemetry. \n\n" +
                    "RECOMMENDATION: Routine UI overhead.",
                2010 or 2011 => ResourceString.GetString("diag_analysis_2010") ??
                    "Windows Defender real-time protection successfully initiated a system scan or mitigated a low-level threat. \n\n" +
                    "RECOMMENDATION: Review Windows Security history for threat details.",
                (>= 2012 and <= 2020) => ResourceString.GetString("diag_analysis_2012") ??
                    "Generic Windows Defender background process telemetry. \n\n" +
                    "RECOMMENDATION: Routine security overhead.",
                2021 or 2022 => ResourceString.GetString("diag_analysis_2021") ??
                    "Lanman Server (SMB) Network share connection drop. A remote client unexpectedly disconnected from a hosted file share. \n\n" +
                    "RECOMMENDATION: Verify local network stability if file sharing is heavily used.",
                (>= 2023 and <= 2048) => ResourceString.GetString("diag_analysis_2023") ??
                    "Generic SMB sharing, routing, and background service logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                2049 or 2050 => ResourceString.GetString("diag_analysis_2049") ??
                    "Storage Spaces or RAID logical partition fault. A logical drive in a mirrored or parity space has degraded or dropped offline. \n\n" +
                    "RECOMMENDATION: Review Windows Storage Spaces health immediately.",
                (>= 2051 and <= 2099) => ResourceString.GetString("diag_analysis_2051") ??
                    "Generic storage allocation and pool management telemetry. \n\n" +
                    "RECOMMENDATION: Routine disk tracking.",
                2100 or 2101 or 2102 => ResourceString.GetString("diag_analysis_2100") ??
                    "Component Based Servicing (CBS) staging failure. A critical system package failed verification. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully clear the update cache and restart the BITS daemon.",
                (>= 2103 and <= 2499) => ResourceString.GetString("diag_analysis_2103") ??
                    "Generic CBS servicing and component cleanup telemetry. \n\n" +
                    "RECOMMENDATION: Routine update maintenance.",
                2504 or 2505 or 2506 or 2507 or 2508 or 2509 => ResourceString.GetString("diag_analysis_2504") ??
                    "Lanman Server / SMB Sharing. The server could not bind to the network transport, or a network name conflict was detected. \n\n" +
                    "RECOMMENDATION: Ensure your computer name is unique on the local network.",
                (>= 2510 and <= 2999) => ResourceString.GetString("diag_analysis_2510") ??
                    "Generic network sharing and background transport telemetry. \n\n" +
                    "RECOMMENDATION: Routine OS overhead.",
                (>= 3000 and <= 3004) => ResourceString.GetString("diag_analysis_3000") ??
                    "Windows Search Indexer parsing error. A specific file type or corrupted directory caused the search crawler to crash. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a restart of the WSearch service.",
                3005 => ResourceString.GetString("diag_analysis_3005") ??
                    "Windows Search Indexer completed a background crawl. \n\n" +
                    "RECOMMENDATION: Routine indexing telemetry.",
                3006 or 3007 => ResourceString.GetString("diag_analysis_3006") ??
                    "The Windows Search service encountered a fatal indexing lock or protocol handler failure. File searching will be slow or unresponsive. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a graceful restart of the WSearch daemon.",
                (>= 3008 and <= 3999) => ResourceString.GetString("diag_analysis_3008") ??
                    "Generic Search, Indexing, and Background Protocol Handler logs. \n\n" +
                    "RECOMMENDATION: Routine search telemetry.",

                // 4000 - 4599: GPO, STORE, & TCP/IP

                (>= 4000 and <= 4003) => ResourceString.GetString("diag_analysis_4000") ??
                    "Generic Group Policy Object (GPO) background synchronization logs. \n\n" +
                    "RECOMMENDATION: Routine active directory telemetry.",
                4004 or 4005 => ResourceString.GetString("diag_analysis_4004") ??
                    "Microsoft Store or MSI Licensing staging error. The local machine's licensing cache is out of sync with the digital payload. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a WSReset.",
                4006 => ResourceString.GetString("diag_analysis_4006") ??
                    "Microsoft Store background license check completed successfully. \n\n" +
                    "RECOMMENDATION: Routine DRM verification.",
                4007 or 4008 => ResourceString.GetString("diag_analysis_4007") ??
                    "Microsoft Store application package deployment or dependency resolution failure. \n\n" +
                    "RECOMMENDATION: Ensure Windows is fully updated to support the targeted AppX version.",
                (>= 4009 and <= 4100) => ResourceString.GetString("diag_analysis_4009") ??
                    "Directory Services or Group Policy Sync Failure. The system cannot reach the Domain Controller, or the Kerberos ticket has expired/desynchronized. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a hard GPUpdate /force and flush the Kerberos ticket cache.",
                4101 => ResourceString.GetString("diag_analysis_4101") ??
                    "Display Driver Warning. The Desktop Window Manager (DWM) detected a TDR (Timeout Detection and Recovery) event. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to safely invalidate the display buffer and reset the driver state.",
                4102 or 4103 => ResourceString.GetString("diag_analysis_4102") ??
                    "PowerShell Module Logging trace. A PowerShell module was loaded into memory. \n\n" +
                    "RECOMMENDATION: Routine auditing telemetry.",
                (>= 4104 and <= 4108) => ResourceString.GetString("diag_analysis_4104") ??
                    "PowerShell Script Block Logging. A highly obfuscated script block or background automation task was executed via the PowerShell host. If unprompted, this is a strong Indicator of Compromise (IoC) for fileless malware. \n\n" +
                    "RECOMMENDATION: Review the script block payload. Click 'Fix' to restrict the PowerShell execution policy.",
                4109 => ResourceString.GetString("diag_analysis_4109") ??
                    "Display Driver Warning. The graphics hardware failed to respond to the OS within the allotted timeframe, causing the driver stack to crash and reset. \n\n" +
                    "RECOMMENDATION: Update your GPU drivers.",
                (>= 4110 and <= 4114) => ResourceString.GetString("diag_analysis_4110") ??
                    "Generic display or UI rendering composition logs. \n\n" +
                    "RECOMMENDATION: Routine visual overhead.",
                4115 => ResourceString.GetString("diag_analysis_4115") ??
                    "Display Driver Recovery. The graphics driver has successfully recovered from a fatal hardware hang. \n\n" +
                    "RECOMMENDATION: Monitor graphics stability during high-load 3D applications.",
                (>= 4116 and <= 4225) => ResourceString.GetString("diag_analysis_4116") ??
                    "Generic diagnostic, UI, and background application tracing logs. \n\n" +
                    "RECOMMENDATION: Routine system telemetry.",
                4226 => ResourceString.GetString("diag_analysis_4226") ??
                    "TCP/IP Limit Reached. The system has reached its half-open connection limit, actively dropping new outbound connections. \n\n" +
                    "RECOMMENDATION: Run a malware scan, as this is typical of botnet or P2P behavior.",
                4227 => ResourceString.GetString("diag_analysis_4227") ??
                    "TCP/IP Ephemeral Port Exhaustion. The system has run out of available network ports. An aggressive background application is opening thousands of connections and not closing them. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a complete network stack teardown and reset the Winsock catalog.",
                (>= 4228 and <= 4230) => ResourceString.GetString("diag_analysis_4228") ??
                    "Generic TCP/IP routing or transport driver background telemetry. \n\n" +
                    "RECOMMENDATION: Routine network tracing.",
                4231 => ResourceString.GetString("diag_analysis_4231") ??
                    "A request to allocate an ephemeral port number from the global TCP port space failed due to all such ports being in use. \n\n" +
                    "RECOMMENDATION: Reboot the machine to clear orphaned TCP connections.",
                (>= 4232 and <= 4318) => ResourceString.GetString("diag_analysis_4232") ??
                    "Generic network tunneling, encapsulation, or background IP helper logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                4319 => ResourceString.GetString("diag_analysis_4319") ??
                    "TCP/IP Conflict Detected. Another device on the local network is actively using the same IPv4 address assigned to this machine. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the IP configuration and request a new DHCP lease.",
                (>= 4320 and <= 4607) => ResourceString.GetString("diag_analysis_4320") ??
                    "Broad range of background networking, IPsec, and minor diagnostic framework states. \n\n" +
                    "RECOMMENDATION: Routine OS telemetry. No action required.",

                // 4600 - 4999: ADVANCED SECURITY, AUDITING, & LOGON EVENTS

                4608 => ResourceString.GetString("diag_analysis_4608") ??
                    "Security Subsystem Startup (Event 4608). The Local Security Authority (LSA) has successfully booted and initialized the auditing subsystem. " +
                    "This is the very first security event logged during a normal Windows boot sequence. \n\n" +
                    "RECOMMENDATION: Routine system startup telemetry. No action required.",
                (>= 4609 and <= 4623) => ResourceString.GetString("diag_analysis_4609") ??
                    "Generic Local Security Authority (LSA) transition states or cryptographic API startup routines. \n\n" +
                    "RECOMMENDATION: Routine security tracking.",
                4624 => ResourceString.GetString("diag_analysis_4624") ??
                    "Successful Logon (Event 4624). A user or background service successfully authenticated and established a session token. " +
                    "This details the Logon Type (e.g., Type 2 Interactive, Type 3 Network, Type 10 RemoteInteractive). \n\n" +
                    "RECOMMENDATION: Routine access telemetry. Verify Logon Types if suspicious remote access is suspected.",
                4625 => ResourceString.GetString("diag_analysis_4625") ??
                    "Failed Logon Attempt (Event 4625). An account failed to log in due to a bad password, unknown username, or blocked account status. " +
                    "Frequent, rapid repetitions of this ID indicate a brute-force attack on your local accounts or exposed RDP ports. \n\n" +
                    "RECOMMENDATION: Review security logs. Consider disabling external Remote Desktop access if unused.",
                (>= 4626 and <= 4633) => ResourceString.GetString("diag_analysis_4626") ??
                    "Generic background authentication package processing or credential validation caching. \n\n" +
                    "RECOMMENDATION: Routine authentication telemetry.",
                4634 => ResourceString.GetString("diag_analysis_4634") ??
                    "Successful Logoff (Event 4634). An account was logged off successfully, and its associated logon session token was destroyed by the LSA. \n\n" +
                    "RECOMMENDATION: Routine telemetry. No action required.",
                (>= 4635 and <= 4646) => ResourceString.GetString("diag_analysis_4635") ??
                    "Generic logon session teardown and token revocation tracing. \n\n" +
                    "RECOMMENDATION: Routine security overhead.",
                4647 => ResourceString.GetString("diag_analysis_4647") ??
                    "User Initiated Logoff (Event 4647). A user explicitly clicked the logoff button, initiating a graceful session termination. \n\n" +
                    "RECOMMENDATION: Routine telemetry. No action required.",
                4648 => ResourceString.GetString("diag_analysis_4648") ??
                    "Explicit Credential Logon (Event 4648). A logon was attempted using explicit credentials. " +
                    "A process elevated its integrity level or a user ran a program as a different user (RunAs). \n\n" +
                    "RECOMMENDATION: Routine security auditing telemetry. Monitor if unexpected privilege escalation is detected.",
                (>= 4649 and <= 4671) => ResourceString.GetString("diag_analysis_4649") ??
                    "Generic token modification, replay attack detection, or credential delegation logging. \n\n" +
                    "RECOMMENDATION: High-level security tracking. Usually benign.",
                4672 => ResourceString.GetString("diag_analysis_4672") ??
                    "Special Privileges Assigned (Event 4672). An Administrator account or a high-privilege system account successfully authenticated. " +
                    "This grants the session token powerful rights like SeDebugPrivilege or SeTakeOwnershipPrivilege. \n\n" +
                    "RECOMMENDATION: Routine telemetry for admin logons. If an unauthorized user generates this event, immediate investigation is required.",
                (>= 4673 and <= 4687) => ResourceString.GetString("diag_analysis_4673") ??
                    "Generic object access, privilege use, or process tracking initiation logs. \n\n" +
                    "RECOMMENDATION: Routine security overhead.",
                4688 => ResourceString.GetString("diag_analysis_4688") ??
                    "Process Creation Audit (Event 4688). A new process has been created and logged by the Windows Security subsystem. " +
                    "This captures the executable name, the parent process (Creator Process ID), and the exact command-line arguments used during invocation. \n\n" +
                    "RECOMMENDATION: Critical for tracking malware execution. Review the command-line arguments for suspicious payloads.",
                4689 => ResourceString.GetString("diag_analysis_4689") ??
                    "Process Termination Audit (Event 4689). A previously tracked process has exited or been forcefully terminated. \n\n" +
                    "RECOMMENDATION: Routine process telemetry.",
                (>= 4690 and <= 4718) => ResourceString.GetString("diag_analysis_4690") ??
                    "Generic process, thread, and handle duplication tracking logs. \n\n" +
                    "RECOMMENDATION: Routine process execution telemetry.",
                4719 => ResourceString.GetString("diag_analysis_4719") ??
                    "System Audit Policy Changed (Event 4719). The global security auditing configuration was modified. " +
                    "Attackers often disable auditing to hide their tracks before executing malicious payloads. \n\n" +
                    "RECOMMENDATION: Verify that this policy change was authorized by a system administrator.",
                4720 => ResourceString.GetString("diag_analysis_4720") ??
                    "User Account Created (Event 4720). A new local or domain user account was added to the Security Account Manager (SAM) or Active Directory. \n\n" +
                    "RECOMMENDATION: Verify that this account creation was explicitly authorized.",
                4721 => ResourceString.GetString("diag_analysis_4721") ??
                    "User Account Enabled. A previously disabled account was toggled back to an active state. \n\n" +
                    "RECOMMENDATION: Ensure this action was authorized.",
                4722 => ResourceString.GetString("diag_analysis_4722") ??
                    "User Account Enabled (Detailed). A previously disabled user account was re-enabled by an administrator. \n\n" +
                    "RECOMMENDATION: Ensure this action was authorized.",
                4723 => ResourceString.GetString("diag_analysis_4723") ??
                    "Password Change Attempt (Event 4723). An attempt was made to change an account's password by the user themselves. \n\n" +
                    "RECOMMENDATION: Routine audit telemetry.",
                4724 => ResourceString.GetString("diag_analysis_4724") ??
                    "Password Reset (Event 4724). An attempt was made to forcefully reset an account's password by an administrator. \n\n" +
                    "RECOMMENDATION: Verify administrator authorization.",
                4725 => ResourceString.GetString("diag_analysis_4725") ??
                    "User Account Disabled (Event 4725). A user account was locked or disabled by an administrator or automated policy. \n\n" +
                    "RECOMMENDATION: Ensure this action was intentional.",
                4726 => ResourceString.GetString("diag_analysis_4726") ??
                    "User Account Deleted (Event 4726). A user account was permanently deleted from the local SAM or Active Directory database. \n\n" +
                    "RECOMMENDATION: Verify authorization.",
                4727 => ResourceString.GetString("diag_analysis_4727") ??
                    "A security-enabled global group was created. \n\n" +
                    "RECOMMENDATION: Routine AD telemetry.",
                4728 => ResourceString.GetString("diag_analysis_4728") ??
                    "Member Added to Global Security Group (Event 4728). A user was added to a powerful domain-level security group. \n\n" +
                    "RECOMMENDATION: Monitor group modifications carefully for unauthorized privilege escalation.",
                (>= 4729 and <= 4731) => ResourceString.GetString("diag_analysis_4729") ??
                    "Generic global security group modification or deletion logs. \n\n" +
                    "RECOMMENDATION: Routine AD tracking.",
                4732 => ResourceString.GetString("diag_analysis_4732") ??
                    "Member Added to Local Security Group (Event 4732). A user was added to a local security group (e.g., adding a user to the local Administrators group). \n\n" +
                    "RECOMMENDATION: Review group modifications carefully. Unauthorized additions to 'Administrators' is a severe security breach.",
                4733 => ResourceString.GetString("diag_analysis_4733") ??
                    "Member Removed from Local Security Group (Event 4733). A user was stripped of their local group membership. \n\n" +
                    "RECOMMENDATION: Routine identity management.",
                4734 => ResourceString.GetString("diag_analysis_4734") ??
                    "A security-enabled local group was deleted. \n\n" +
                    "RECOMMENDATION: Routine group management.",
                4735 => ResourceString.GetString("diag_analysis_4735") ??
                    "Local Group Modified (Event 4735). The properties of a security-enabled local group were altered. \n\n" +
                    "RECOMMENDATION: Routine tracking.",
                4736 or 4737 => ResourceString.GetString("diag_analysis_4736") ??
                    "Generic unmapped group modification tracking logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                4738 => ResourceString.GetString("diag_analysis_4738") ??
                    "User Account Modified (Event 4738). A user account property (e.g., password never expires, logon hours, profile path) was altered. \n\n" +
                    "RECOMMENDATION: Ensure this modification was authorized.",
                4739 => ResourceString.GetString("diag_analysis_4739") ??
                    "Domain Policy Modified. The LSA domain policy was altered. \n\n" +
                    "RECOMMENDATION: Verify domain controller administration.",
                4740 => ResourceString.GetString("diag_analysis_4740") ??
                    "Account Lockout (Event 4740). An account exceeded the failed login threshold and has been locked out to prevent brute-force cracking. \n\n" +
                    "RECOMMENDATION: Review security logs for automated brute-force attempts targeting this specific account.",
                (>= 4741 and <= 4755) => ResourceString.GetString("diag_analysis_4741") ??
                    "Generic computer account, local group, and universal group modification logs. \n\n" +
                    "RECOMMENDATION: Routine Active Directory telemetry.",
                4756 => ResourceString.GetString("diag_analysis_4756") ??
                    "Member Added to Universal Security Group (Event 4756). A user was granted access to an AD Universal Group. \n\n" +
                    "RECOMMENDATION: Monitor for unauthorized forest-wide privilege escalation.",
                (>= 4757 and <= 4767) => ResourceString.GetString("diag_analysis_4757") ??
                    "Generic universal group and SID history tracking logs. \n\n" +
                    "RECOMMENDATION: Routine AD tracking.",
                4768 => ResourceString.GetString("diag_analysis_4768") ??
                    "Kerberos TGT Requested (Event 4768). A Ticket Granting Ticket was successfully requested from the Key Distribution Center (KDC). " +
                    "This is the fundamental proof of a successful domain logon. \n\n" +
                    "RECOMMENDATION: Routine authentication telemetry.",
                4769 => ResourceString.GetString("diag_analysis_4769") ??
                    "Kerberos Service Ticket Requested (Event 4769). A user requested access to a specific network service or server using their TGT. \n\n" +
                    "RECOMMENDATION: Routine lateral network access telemetry.",
                4770 => ResourceString.GetString("diag_analysis_4770") ??
                    "A Kerberos service ticket was renewed. \n\n" +
                    "RECOMMENDATION: Routine authentication maintenance.",
                4771 => ResourceString.GetString("diag_analysis_4771") ??
                    "Kerberos Pre-Authentication Failed (Event 4771). A network identity token was rejected by the KDC, usually due to a bad password or severe time desynchronization. \n\n" +
                    "RECOMMENDATION: Verify domain controller connectivity and local time sync.",
                (>= 4772 and <= 4775) => ResourceString.GetString("diag_analysis_4772") ??
                    "Generic Kerberos ticketing and account mapping failure logs. \n\n" +
                    "RECOMMENDATION: Routine domain authentication tracking.",
                4776 => ResourceString.GetString("diag_analysis_4776") ??
                    "NTLM Authentication (Event 4776). The computer attempted to validate the credentials for an account using the legacy NTLM protocol instead of Kerberos. \n\n" +
                    "RECOMMENDATION: Monitor NTLM usage, as it is vulnerable to relay attacks (Pass-the-Hash).",
                4777 => ResourceString.GetString("diag_analysis_4777") ??
                    "The domain controller failed to validate the credentials for an account using NTLM. \n\n" +
                    "RECOMMENDATION: Check for bad passwords or disabled legacy protocols.",
                4778 => ResourceString.GetString("diag_analysis_4778") ??
                    "Session Reconnected (Event 4778). A user reconnected to a disconnected Terminal Services or Fast User Switching session. \n\n" +
                    "RECOMMENDATION: Routine session telemetry.",
                4779 => ResourceString.GetString("diag_analysis_4779") ??
                    "Session Disconnected (Event 4779). A user disconnected from a Terminal Services session without explicitly logging off. \n\n" +
                    "RECOMMENDATION: Routine session telemetry.",
                (>= 4780 and <= 4797) => ResourceString.GetString("diag_analysis_4780") ??
                    "Generic ACL, password policy, and credential manager tracking logs. \n\n" +
                    "RECOMMENDATION: Routine security tracking.",
                4798 => ResourceString.GetString("diag_analysis_4798") ??
                    "Local Group Enumeration (Event 4798). A process or user enumerated the members of a local security group. " +
                    "Attackers often do this during the discovery phase (e.g., 'net localgroup administrators'). \n\n" +
                    "RECOMMENDATION: Verify if the enumeration was triggered by an authorized administrative script.",
                4799 => ResourceString.GetString("diag_analysis_4799") ??
                    "Local Group Membership Enumeration (Event 4799). A process requested the group memberships of a specific user. \n\n" +
                    "RECOMMENDATION: Routine telemetry, but can indicate attacker discovery activity.",
                4800 => ResourceString.GetString("diag_analysis_4800") ??
                    "Workstation Locked (Event 4800). The user locked the console (Win + L). \n\n" +
                    "RECOMMENDATION: Routine physical security telemetry.",
                4801 => ResourceString.GetString("diag_analysis_4801") ??
                    "Workstation Unlocked (Event 4801). The user successfully unlocked the console. \n\n" +
                    "RECOMMENDATION: Routine physical security telemetry.",
                (>= 4802 and <= 4999) => ResourceString.GetString("diag_analysis_4802") ??
                    "Security Group Enumeration or ACL Modification tracking. File or object permissions were actively modified on the NTFS partition. \n\n" +
                    "RECOMMENDATION: Routine disk security auditing.",

                // 5000 - 19999: NETWORKING, STORE, & HYPER-V

                (>= 5000 and <= 5004) => ResourceString.GetString("diag_analysis_5000") ??
                    "Network isolation or Microsoft Store licensing telemetry. \n\n" +
                    "RECOMMENDATION: Routine OS overhead.",
                5005 => ResourceString.GetString("diag_analysis_5005") ??
                    "WLAN AutoConfig: A wireless network was successfully connected. \n\n" +
                    "RECOMMENDATION: Routine Wi-Fi telemetry.",
                5006 => ResourceString.GetString("diag_analysis_5006") ??
                    "Network profile transition. The system shifted from a Public to a Private firewall profile (or vice versa). \n\n" +
                    "RECOMMENDATION: Verify the correct firewall profile is active.",
                5007 => ResourceString.GetString("diag_analysis_5007") ??
                    "WLAN AutoConfig: A wireless network was disconnected. \n\n" +
                    "RECOMMENDATION: Routine Wi-Fi telemetry.",
                5008 or 5009 => ResourceString.GetString("diag_analysis_5008") ??
                    "Generic Network isolation or firewall configuration logs. \n\n" +
                    "RECOMMENDATION: Routine tracking.",
                5010 => ResourceString.GetString("diag_analysis_5010") ??
                    "WLAN AutoConfig: Wireless network isolation drop. The adapter failed to establish a secure connection. \n\n" +
                    "RECOMMENDATION: Check router security protocols.",
                5011 or 5012 => ResourceString.GetString("diag_analysis_5011") ??
                    "Network Adapter or Windows Service host transition states. \n\n" +
                    "RECOMMENDATION: Routine service telemetry.",
                (>= 5013 and <= 5031) => ResourceString.GetString("diag_analysis_5013") ??
                    "Generic network tunneling and IP Helper diagnostic logging. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                5032 => ResourceString.GetString("diag_analysis_5032") ??
                    "Windows Firewall or Network isolation actively blocked a process from broadcasting on the local subnet. \n\n" +
                    "RECOMMENDATION: Check Firewall rules if network discovery is failing.",
                (>= 5033 and <= 5139) => ResourceString.GetString("diag_analysis_5033") ??
                    "Broad range of legacy networking, IPsec, and minor diagnostic framework states. \n\n" +
                    "RECOMMENDATION: Routine OS telemetry. No action required.",
                5140 => ResourceString.GetString("diag_analysis_5140") ??
                    "Network Share Accessed (Event 5140). A network share object was successfully accessed by a local or remote user. \n\n" +
                    "RECOMMENDATION: Routine SMB telemetry.",
                5141 => ResourceString.GetString("diag_analysis_5141") ??
                    "Network Share Access Denied. A user attempted to access a file share but lacked the necessary ACL permissions. \n\n" +
                    "RECOMMENDATION: Verify user group permissions on the target share.",
                5142 => ResourceString.GetString("diag_analysis_5142") ??
                    "Network Share Added. A new SMB file share was created and broadcasted on the network. \n\n" +
                    "RECOMMENDATION: Ensure the share creation was authorized.",
                5143 or 5144 => ResourceString.GetString("diag_analysis_5143") ??
                    "Network Share Modified or Deleted. The properties or existence of an SMB share were altered. \n\n" +
                    "RECOMMENDATION: Verify authorization.",
                5145 => ResourceString.GetString("diag_analysis_5145") ??
                    "Network Share Detailed Access (Event 5145). A network share object was accessed, and the system logged the specific file/folder being interacted with. \n\n" +
                    "RECOMMENDATION: Critical telemetry for tracking lateral movement or data exfiltration over SMB.",
                (>= 5146 and <= 5599) => ResourceString.GetString("diag_analysis_5146") ??
                    "Generic advanced auditing, IPsec, and network policy server logs. \n\n" +
                    "RECOMMENDATION: Routine security tracking.",
                (>= 5600 and <= 5699) => ResourceString.GetString("diag_analysis_5600") ??
                    "Windows Management Instrumentation (WMI) repository corruption or provider timeout. A background application requested system data, but the WMI provider crashed. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to run a deep salvaging operation on the WMI repository.",
                (>= 5700 and <= 5718) => ResourceString.GetString("diag_analysis_5700") ??
                    "Generic Netlogon and domain controller discovery telemetry. \n\n" +
                    "RECOMMENDATION: Routine AD tracking.",
                5719 => ResourceString.GetString("diag_analysis_5719") ??
                    "Netlogon Failure. The system attempted to authenticate with a Domain Controller or network resource, but the server was unreachable. \n\n" +
                    "RECOMMENDATION: Verify your DNS settings and ensure your network adapter has a valid IP lease.",
                (>= 5720 and <= 6004) => ResourceString.GetString("diag_analysis_5720") ??
                    "Generic Netlogon, LSA, and domain authentication tracking logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                6005 => ResourceString.GetString("diag_analysis_6005") ??
                    "Event Log Service Started. The system has officially entered the OS initialization phase. \n\n" +
                    "RECOMMENDATION: Routine boot marker.",
                6007 => ResourceString.GetString("diag_analysis_6007") ??
                    "Generic Event Log service configuration tracking. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                6009 => ResourceString.GetString("diag_analysis_6009") ??
                    "System Boot Information. The Event Log service recorded the Windows version, build number, and service pack level during boot. \n\n" +
                    "RECOMMENDATION: Routine boot marker.",
                (>= 6010 and <= 6061) => ResourceString.GetString("diag_analysis_6010") ??
                    "Generic Winlogon, system notification, and event logging framework tracking. \n\n" +
                    "RECOMMENDATION: Routine OS overhead.",
                6062 => ResourceString.GetString("diag_analysis_6062") ??
                    "WLAN AutoConfig: A wireless interface was successfully powered on and initialized. \n\n" +
                    "RECOMMENDATION: Routine Wi-Fi telemetry.",
                (>= 6063 and <= 6271) => ResourceString.GetString("diag_analysis_6063") ??
                    "Generic WLAN, Netlogon, and background service diagnostics. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                6272 or 6273 => ResourceString.GetString("diag_analysis_6272") ??
                    "Network Policy Server (NPS) granted or denied access to a user. \n\n" +
                    "RECOMMENDATION: Review RADIUS authentication logs.",
                (>= 6274 and <= 6277) => ResourceString.GetString("diag_analysis_6274") ??
                    "Generic NPS and network policy evaluation tracking. \n\n" +
                    "RECOMMENDATION: Routine network security telemetry.",
                6278 => ResourceString.GetString("diag_analysis_6278") ??
                    "Network Policy Server (NPS) quarantined a connection. The client failed health checks (e.g., outdated antivirus) and was placed in a restricted network. \n\n" +
                    "RECOMMENDATION: Update the client machine to comply with NAC policies.",
                (>= 6279 and <= 7999) => ResourceString.GetString("diag_analysis_6279") ??
                    "Massive block of generic service control, active directory, and network policy server state transitions. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry. No action required.",
                8000 => ResourceString.GetString("diag_analysis_8000") ??
                    "AppLocker Policy Enforcement. An executable was allowed to run based on a local security policy. \n\n" +
                    "RECOMMENDATION: Routine auditing telemetry.",
                8001 => ResourceString.GetString("diag_analysis_8001") ??
                    "AppLocker: An executable was blocked from running due to an explicit AppLocker deny rule or missing publisher signature. \n\n" +
                    "RECOMMENDATION: Review local Group Policy object constraints.",
                8002 => ResourceString.GetString("diag_analysis_8002") ??
                    "AppLocker: A script or MSI package was actively blocked from execution. \n\n" +
                    "RECOMMENDATION: If authorized, adjust the AppLocker script rules.",
                8003 => ResourceString.GetString("diag_analysis_8003") ??
                    "AppLocker: A packaged app (UWP) was blocked from launching. \n\n" +
                    "RECOMMENDATION: Verify the application's digital signature is intact.",
                8004 => ResourceString.GetString("diag_analysis_8004") ??
                    "AppLocker: A DLL was prevented from loading into memory to protect the host process. \n\n" +
                    "RECOMMENDATION: Ensure the application dependencies are secure.",
                (>= 8005 and <= 8040) => ResourceString.GetString("diag_analysis_8005") ??
                    "Code Integrity Block. Windows actively prevented a script or DLL from mapping into memory because it violates a local security policy. \n\n" +
                    "RECOMMENDATION: If this application is trusted, review local AppLocker rules or disable Smart App Control.",
                (>= 8041 and <= 8192) => ResourceString.GetString("diag_analysis_8041") ??
                    "Generic Code Integrity, AppLocker, and SmartScreen tracking logs. \n\n" +
                    "RECOMMENDATION: Routine security tracking.",
                8193 or 8194 => ResourceString.GetString("diag_analysis_8193") ??
                    "Volume Shadow Copy Service (VSS) failed to snapshot the drive. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to hard-reset the VSS and Software Shadow Copy Provider services.",
                (>= 8195 and <= 8212) => ResourceString.GetString("diag_analysis_8195") ??
                    "Generic VSS provider background staging logs. \n\n" +
                    "RECOMMENDATION: Routine backup telemetry.",
                8213 => ResourceString.GetString("diag_analysis_8213") ??
                    "VSS Writer failed during the PrepareForSnapshot phase. The background application could not lock its data in time. \n\n" +
                    "RECOMMENDATION: Restart the VSS provider.",
                (>= 8214 and <= 8216) => ResourceString.GetString("diag_analysis_8214") ??
                    "Generic VSS writer diagnostic tracing. \n\n" +
                    "RECOMMENDATION: Routine backup telemetry.",
                (>= 8217 and <= 8223) => ResourceString.GetString("diag_analysis_8217") ??
                    "Advanced Volume Shadow Copy (VSS) Writer Desynchronization. A background backup software interrupted a snapshot generation. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to hard-reset the VSS framework.",
                8224 => ResourceString.GetString("diag_analysis_8224") ??
                    "The VSS service successfully shut down due to idle timeout. \n\n" +
                    "RECOMMENDATION: Routine service telemetry.",
                8225 or 8226 => ResourceString.GetString("diag_analysis_8225") ??
                    "VSS Writer generated an error while attempting to freeze or thaw the I/O pipeline on the target disk. \n\n" +
                    "RECOMMENDATION: Investigate storage hardware health.",
                (>= 8227 and <= 8999) => ResourceString.GetString("diag_analysis_8227") ??
                    "Generic VSS, storage pool, and background backup provider tracing. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                9000 => ResourceString.GetString("diag_analysis_9000") ??
                    "Desktop Window Manager (DWM) composition engine initialization. \n\n" +
                    "RECOMMENDATION: Routine UI startup.",
                9001 or 9002 => ResourceString.GetString("diag_analysis_9001") ??
                    "Desktop Window Manager (DWM) Severe Resource Exhaustion. The UI engine has completely run out of allocatable heap memory. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a full native cache purge.",
                (>= 9003 and <= 9009) => ResourceString.GetString("diag_analysis_9003") ??
                    "Remote Desktop Services (TermService) encryption downgrade or protocol timeout. \n\n" +
                    "RECOMMENDATION: Ensure NLA (Network Level Authentication) is configured correctly.",
                (>= 9010 and <= 9099) => ResourceString.GetString("diag_analysis_9010") ??
                    "Remote Desktop Services connection anomaly. The system experienced licensing threshold exhaustion or an abrupt socket disconnect. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the RDP listener stack and regenerate the self-signed certificates.",
                (>= 9100 and <= 9999) => ResourceString.GetString("diag_analysis_9100") ??
                    "Generic Remote Desktop, DWM, and Terminal Services background tracking logs. \n\n" +
                    "RECOMMENDATION: Routine OS overhead.",
                10000 or 10001 => ResourceString.GetString("diag_analysis_10000") ??
                    "WLAN / Network adapter power management blocked a sleep transition. \n\n" +
                    "RECOMMENDATION: Disable 'Allow this device to wake the computer' on your network adapters.",
                10002 or 10003 => ResourceString.GetString("diag_analysis_10002") ??
                    "WLAN AutoConfig: A wireless connection was disconnected or failed to negotiate an 802.11 association. \n\n" +
                    "RECOMMENDATION: Forget the Wi-Fi network and reconnect.",
                (>= 10004 and <= 10010) => ResourceString.GetString("diag_analysis_10004") ??
                    "WLAN AutoConfig: Wireless profile mismatch or corrupted XML configuration in the saved networks list. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush corrupted Wi-Fi profiles.",
                10011 or 10012 => ResourceString.GetString("diag_analysis_10011") ??
                    "Network Adapter or Windows Service host transition states. \n\n" +
                    "RECOMMENDATION: Routine service telemetry.",
                (>= 10013 and <= 10015) => ResourceString.GetString("diag_analysis_10013") ??
                    "Microsoft Store or AppX deployment background validation checks. \n\n" +
                    "RECOMMENDATION: Routine Store telemetry.",
                10016 => ResourceString.GetString("diag_analysis_10016") ??
                    "Distributed COM (DCOM) Permission Error. A background app attempted to launch a DCOM server but lacked Local Activation permissions. " +
                    "This is extremely common in modern Windows and generally benign. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to automatically reconcile known DCOM registry ACLs.",
                (>= 10017 and <= 10019) => ResourceString.GetString("diag_analysis_10017") ??
                    "Generic DCOM or RPC endpoint mapping telemetry. \n\n" +
                    "RECOMMENDATION: Routine OS overhead.",
                (>= 10020 and <= 10022) => ResourceString.GetString("diag_analysis_10020") ??
                    "DHCP Client: The network adapter failed to renew its IP address lease from the DHCP server, or a conflict was detected. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully release and renew the IP address.",
                (>= 10023 and <= 10052) => ResourceString.GetString("diag_analysis_10023") ??
                    "Generic DHCP, Netlogon, and network transition state logging. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                10053 or 10054 => ResourceString.GetString("diag_analysis_10053") ??
                    "Winsock Connection Aborted or Reset by Peer. An established connection was forcefully terminated. \n\n" +
                    "RECOMMENDATION: Check antivirus or firewall blocking rules.",
                (>= 10055 and <= 10064) => ResourceString.GetString("diag_analysis_10055") ??
                    "Generic Winsock, network proxy, and transport layer background logs. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                (>= 10065 and <= 10074) => ResourceString.GetString("diag_analysis_10065") ??
                    "Layer 2 Network Disconnect. The physical network adapter or Wi-Fi radio reported an unexpected link state drop. \n\n" +
                    "RECOMMENDATION: Verify physical Ethernet connections or Wi-Fi signal integrity.",
                (>= 10075 and <= 10099) => ResourceString.GetString("diag_analysis_10075") ??
                    "Generic Layer 2, MAC filtering, and NDIS driver transition logs. \n\n" +
                    "RECOMMENDATION: Routine OS telemetry.",
                (>= 10100 and <= 10120) => ResourceString.GetString("diag_analysis_10100") ??
                    "Driver Power State Failure. A device driver failed to transition into or out of a sleep/hibernation state in a timely manner. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the hibernation file and rebuild the Fast Startup cache.",
                (>= 10121 and <= 10199) => ResourceString.GetString("diag_analysis_10121") ??
                    "Generic kernel power and ACPI driver polling events. \n\n" +
                    "RECOMMENDATION: Routine power management tracking.",
                10200 => ResourceString.GetString("diag_analysis_10200") ??
                    "WLAN AutoConfig service stopped successfully. \n\n" +
                    "RECOMMENDATION: Routine service termination.",
                (>= 10201 and <= 10399) => ResourceString.GetString("diag_analysis_10201") ??
                    "Generic WLAN, WWAN, and Mobile Broadband transition state logging. \n\n" +
                    "RECOMMENDATION: Routine mobile telemetry.",
                10400 => ResourceString.GetString("diag_analysis_10400") ??
                    "NDIS network adapter driver initiated a spontaneous reset on the physical hardware to clear a frozen state. \n\n" +
                    "RECOMMENDATION: Update Network Interface Card (NIC) drivers.",
                (>= 10401 and <= 10999) => ResourceString.GetString("diag_analysis_10401") ??
                    "Broad block of NDIS, TCP/IP, and Windows network diagnostic framework background logs. \n\n" +
                    "RECOMMENDATION: Routine network tracing. No action required.",
                11001 or 11002 or 11004 or 11005 or 11006 => ResourceString.GetString("diag_analysis_11001") ??
                    "Winsock DNS / Host Failure. The socket API received a non-authoritative response or failed to locate the requested server domain. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the DNS resolver cache.",
                11003 => ResourceString.GetString("diag_analysis_11003") ??
                    "DNS Client generic fallback or caching threshold tracking. \n\n" +
                    "RECOMMENDATION: Routine DNS telemetry.",
                (>= 11007 and <= 11705) => ResourceString.GetString("diag_analysis_11007") ??
                    "Massive block of DNS, network tracing, and MSI background staging logs. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.",
                11706 or 11707 or 11708 => ResourceString.GetString("diag_analysis_11706") ??
                    "The Windows Installer (MSI) service encountered a registry lock or metadata corruption while staging a package. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to unregister and cleanly re-register the MSI server module.",
                (>= 11709 and <= 11723) => ResourceString.GetString("diag_analysis_11709") ??
                    "Generic MSI Installer file extraction and directory mapping logs. \n\n" +
                    "RECOMMENDATION: Routine installation tracking.",
                11724 => ResourceString.GetString("diag_analysis_11724") ??
                    "MSI Installer successfully completed a software removal operation. \n\n" +
                    "RECOMMENDATION: Routine software management.",
                (>= 11725 and <= 11727) => ResourceString.GetString("diag_analysis_11725") ??
                    "Generic MSI uninstallation cleanup tracking. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                11728 => ResourceString.GetString("diag_analysis_11728") ??
                    "MSI Installer engine logged a successful uninstallation or repair transaction. \n\n" +
                    "RECOMMENDATION: Routine software management tracking.",
                (>= 11729 and <= 12000) => ResourceString.GetString("diag_analysis_11729") ??
                    "Broad range of MSI Installer and application framework reporting logs. \n\n" +
                    "RECOMMENDATION: Routine application tracking.",
                (>= 12001 and <= 12013) => ResourceString.GetString("diag_analysis_12001") ??
                    "TCP/IP stack or Winsock catalog anomaly. The system encountered a socket binding failure, route metric error, or a deep transport timeout. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a complete network stack teardown (Winsock reset + IP renew).",
                (>= 12014 and <= 12288) => ResourceString.GetString("diag_analysis_12014") ??
                    "Generic Winsock, network, and Volume Shadow Copy background initialization logs. \n\n" +
                    "RECOMMENDATION: Routine system tracking.",
                12289 or 12290 => ResourceString.GetString("diag_analysis_12289") ??
                    "Volume Shadow Copy Service (VSS) failed to snapshot the drive. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to hard-reset the VSS and Software Shadow Copy Provider services.",
                12291 => ResourceString.GetString("diag_analysis_12291") ??
                    "Advanced Volume Shadow Copy (VSS) Writer Desynchronization. A background backup software interrupted a snapshot generation. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to hard-reset the VSS framework.",
                12292 => ResourceString.GetString("diag_analysis_12292") ??
                    "VSS Writer failed to generate an accurate snapshot within the hardcoded timeout period. \n\n" +
                    "RECOMMENDATION: Restart the VSS provider.",
                12293 => ResourceString.GetString("diag_analysis_12293") ??
                    "Volume Shadow Copy Service error. A VSS operation failed due to an invalid XML configuration or missing writer component. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to hard-reset the VSS framework.",
                (>= 12294 and <= 12296) => ResourceString.GetString("diag_analysis_12294") ??
                    "Generic VSS provider diagnostic tracing and state validation logs. \n\n" +
                    "RECOMMENDATION: Routine backup telemetry.",
                12297 or 12298 => ResourceString.GetString("diag_analysis_12297") ??
                    "VSS Writer Desynchronization. A background backup software interrupted a snapshot generation, causing a metadata lock. \n\n" +
                    "RECOMMENDATION: Restart the VSS provider.",
                (>= 12299 and <= 12999) => ResourceString.GetString("diag_analysis_12299") ??
                    "Generic VSS, storage pool, and background backup provider tracing. \n\n" +
                    "RECOMMENDATION: Routine telemetry.",
                (>= 13000 and <= 13003) => ResourceString.GetString("diag_analysis_13000") ??
                    "Hyper-V Virtual Switch drop. A virtualized network interface temporarily lost connection to the physical MAC. \n\n" +
                    "RECOMMENDATION: Review Hyper-V virtual switch manager settings.",
                (>= 13004 and <= 13099) => ResourceString.GetString("diag_analysis_13004") ??
                    "Device Setup Manager (DsmSvc) encountered a metadata retrieval error. Windows is struggling to download device icons or driver packages from the Windows Update catalog. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear the Device Metadata cache and restart the Device Setup Manager.",
                (>= 13100 and <= 13999) => ResourceString.GetString("diag_analysis_13100") ??
                    "Hyper-V Virtual Machine Management anomaly. The hypervisor encountered a state-transition fault or memory allocation failure while managing a guest VM. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to gracefully restart the VMMS daemon and release deadlocked hypervisor resources.",
                (>= 14000 and <= 14999) => ResourceString.GetString("diag_analysis_14000") ??
                    "Broad range of Hyper-V, Storage Spaces, and generic OS diagnostic tracking logs. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry. No action required.",
                (>= 15000 and <= 15150) => ResourceString.GetString("diag_analysis_15000") ??
                    "COM+ Surrogate or Advanced Background Task transition state fault. An out-of-process COM server crashed. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the Explorer shell and clear orphaned COM tasks.",
                (>= 15151 and <= 18499) => ResourceString.GetString("diag_analysis_15151") ??
                    "Massive block of COM+, DCOM, and Hyper-V initialization background tracking logs. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.",
                18500 or 18501 or 18502 => ResourceString.GetString("diag_analysis_18500") ??
                    "Hyper-V VHDX Lock. The system could not mount or unmount a virtual hard disk because the file is locked by another process. \n\n" +
                    "RECOMMENDATION: Restart the host machine to release the locked file handles.",
                (>= 18503 and <= 19000) => ResourceString.GetString("diag_analysis_18503") ??
                    "Hyper-V Integration Services timeout. The guest VM stopped responding to the host heartbeat. \n\n" +
                    "RECOMMENDATION: Check guest OS health and ensure integration services are updated.",

                // 20000+ : FIREWALL, CBS, DO, TCP/IP, & BITLOCKER

                (>= 20000 and <= 20005) => ResourceString.GetString("diag_analysis_20000") ??
                    "Windows Defender Firewall block. A network packet was actively dropped because it did not match an allowed inbound/outbound rule. \n\n" +
                    "RECOMMENDATION: Routine firewall telemetry.",
                (>= 20006 and <= 20300) => ResourceString.GetString("diag_analysis_20006") ??
                    "Base Filtering Engine (BFE) policy anomaly. The packet filtering engine encountered resource exhaustion or a rule conflict. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the Firewall to default state and restart the BFE service.",
                (>= 20301 and <= 20999) => ResourceString.GetString("diag_analysis_20301") ??
                    "Generic Firewall, BFE, and Network Isolation tracking logs. \n\n" +
                    "RECOMMENDATION: Routine security tracking.",
                (>= 21000 and <= 21010) => ResourceString.GetString("diag_analysis_21000") ??
                    "Windows Update: Cryptographic signature verification failed for a downloaded CAB file. The file may have been modified in transit. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush the SoftwareDistribution cache and re-download the payload.",
                (>= 21011 and <= 21050) => ResourceString.GetString("diag_analysis_21011") ??
                    "Windows Update: The downloaded payload metadata is corrupt or incomplete. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear the update cache.",
                (>= 21051 and <= 21100) => ResourceString.GetString("diag_analysis_21051") ??
                    "Windows Update: The setup engine encountered an access denied error while replacing system files. \n\n" +
                    "RECOMMENDATION: Ensure third-party antivirus software is not blocking the TrustedInstaller service.",
                (>= 21101 and <= 21150) => ResourceString.GetString("diag_analysis_21101") ??
                    "Windows Update: A prerequisite Servicing Stack Update (SSU) is missing. The current update cannot install until the underlying CBS engine is updated. \n\n" +
                    "RECOMMENDATION: Run the Windows Update Troubleshooter to fetch the latest SSU.",
                (>= 21151 and <= 21200) => ResourceString.GetString("diag_analysis_21151") ??
                    "Delivery Optimization: Peer-to-peer cache encountered a segment mismatch. The chunk fetched from the local network was corrupted. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear the Delivery Optimization cache.",
                (>= 21201 and <= 21299) => ResourceString.GetString("diag_analysis_21201") ??
                    "Delivery Optimization: HTTP download from the Microsoft Content Catalog timed out. \n\n" +
                    "RECOMMENDATION: Verify your internet connection speed and stability.",
                (>= 21300 and <= 22999) => ResourceString.GetString("diag_analysis_21300") ??
                    "Broad range of CBS, Servicing, and Delivery Optimization generic tracking logs. \n\n" +
                    "RECOMMENDATION: Routine update maintenance telemetry.",
                23000 => ResourceString.GetString("diag_analysis_23000") ??
                    "TCP/IP: The IPv4 routing table was corrupted and rebuilt automatically by the networking stack. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a complete network stack teardown.",
                23001 => ResourceString.GetString("diag_analysis_23001") ??
                    "TCP/IP: A default gateway was dropped due to missing ARP responses. The local router is no longer communicating. \n\n" +
                    "RECOMMENDATION: Restart the local router or switch.",
                23002 => ResourceString.GetString("diag_analysis_23002") ??
                    "TCP/IP: ICMP fragmentation needed but the DF (Don't Fragment) bit was set. A packet was too large for the network MTU. \n\n" +
                    "RECOMMENDATION: Adjust the MTU size on your network adapter.",
                (>= 23003 and <= 23050) => ResourceString.GetString("diag_analysis_23003") ??
                    "TCP/IP: IPv4 interface metric calculation error or duplicate route detected in the routing table. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the TCP/IP stack.",
                (>= 23051 and <= 23100) => ResourceString.GetString("diag_analysis_23051") ??
                    "TCP/IP: IPv6 Neighbor Discovery protocol failed. The system cannot resolve local IPv6 addresses. \n\n" +
                    "RECOMMENDATION: Disable IPv6 if it is not explicitly required by your ISP.",
                (>= 23101 and <= 23150) => ResourceString.GetString("diag_analysis_23101") ??
                    "TCP/IP: Teredo or 6to4 tunneling mechanism failed to establish a relay connection to the broader IPv6 internet. \n\n" +
                    "RECOMMENDATION: Routine tunneling failure. No action required.",
                (>= 23151 and <= 24000) => ResourceString.GetString("diag_analysis_23151") ??
                    "Deep network transport or TCP/IP protocol anomaly. The system encountered routing metric errors or socket layer resets. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a complete network stack teardown.",
                (>= 24001 and <= 24499) => ResourceString.GetString("diag_analysis_24001") ??
                    "Generic TCP/IP, IPsec, and Network Tracing background logs. \n\n" +
                    "RECOMMENDATION: Routine network telemetry.",
                24500 => ResourceString.GetString("diag_analysis_24500") ??
                    "BitLocker: The TPM PCR registers were altered, triggering a recovery key prompt on the next boot cycle. \n\n" +
                    "RECOMMENDATION: Ensure you have your BitLocker recovery key available.",
                24501 => ResourceString.GetString("diag_analysis_24501") ??
                    "BitLocker: The system failed to unwrap the Volume Master Key (VMK). The drive remains securely locked. \n\n" +
                    "RECOMMENDATION: Enter the correct PIN or recovery password.",
                (>= 24502 and <= 24600) => ResourceString.GetString("diag_analysis_24502") ??
                    "BitLocker: The Secure Boot integrity checks failed to validate the boot manager, preventing automatic decryption. \n\n" +
                    "RECOMMENDATION: Verify that Secure Boot is enabled and unaltered in the BIOS.",
                (>= 24601 and <= 24700) => ResourceString.GetString("diag_analysis_24601") ??
                    "BitLocker: Encrypted volume metadata block error or DMA protection violation detected. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcibly refresh the BitLocker Key Protectors.",

                // Absolute Fallback
                _ => string.Format(ResourceString.GetString("diag_analysis_fallback") ??
                     "System Event {0} was logged by '{1}'. While this represents a system notice or error, it falls outside the critical anomaly thresholds. The Remediation Engine is standing by.",
                     eventId, sourceName)
            };
        }

        public static string GenerateHardwareAnalysis(int wmiErrorCode, string deviceName = "Unknown Device")
        {
            return wmiErrorCode switch
            {
                // HIGH-PROFILE SPECIFIC HARDWARE ERRORS

                1 => string.Format(ResourceString.GetString("diag_hw_analysis_1") ??
                    "Code 1 indicates '{0}' has no driver installed or is incorrectly configured. " +
                    "Windows sees the hardware on the physical bus, but the OS lacks the binary instructions required to translate hardware interrupts into software commands. " +
                    "This leaves the device completely orphaned in the device tree.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to trigger a deep PnP enumeration to fetch the missing driver.", deviceName),

                10 => string.Format(ResourceString.GetString("diag_hw_analysis_10") ??
                    "Code 10 is a critical hardware initialization failure. The driver for '{0}' failed to start or load into memory. " +
                    "This usually occurs during the 'DriverEntry' routine when the hardware fails to respond to the driver's initial handshake within the required timeout period. " +
                    "It is highly common with corrupted filter drivers, incompatible hardware revisions, or failing audio/network adapters.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcibly invalidate the driver state and reset the hardware.", deviceName),

                12 => string.Format(ResourceString.GetString("diag_hw_analysis_12") ??
                    "Code 12 is a Resource Conflict. '{0}' cannot find enough free I/O ports, Interrupt Requests (IRQs), or Direct Memory Access (DMA) channels. " +
                    "The Windows Plug and Play (PnP) manager failed to arbitrate resources because another device on the motherboard is hard-locked to the required addresses. " +
                    "This is common on heavily populated PCIe buses.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state and force Windows to reallocate IRQs.", deviceName),

                14 => string.Format(ResourceString.GetString("diag_hw_analysis_14") ??
                    "Code 14 indicates '{0}' cannot work properly until you restart your computer. " +
                    "The device has been successfully installed, but the driver requires a system reboot to initialize low-level kernel hooks or replace in-use system binaries. " +
                    "The device is currently in an intermediary 'pending' state.\n\n" +
                    "RECOMMENDATION: Restart your PC to finalize the device setup.", deviceName),

                19 => string.Format(ResourceString.GetString("diag_hw_analysis_19") ??
                    "Code 19 indicates extreme Registry Hive corruption. The specific registry configuration keys (like LowerFilters or UpperFilters) that control '{0}' are fundamentally broken or missing. " +
                    "The OS cannot build the driver stack because the routing instructions in the registry are invalid.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to strip the corrupted configuration and cleanly rebuild the registry parameters from scratch.", deviceName),

                21 => string.Format(ResourceString.GetString("diag_hw_analysis_21") ??
                    "Code 21 means Windows is actively removing '{0}'. " +
                    "The device is currently in a transitional state (IRP_MN_REMOVE_DEVICE) and cannot be interacted with until the PnP manager completes the teardown of its driver stack.\n\n" +
                    "RECOMMENDATION: Wait a few seconds, or click 'Fix' to force a PnP rescan to clear the queue.", deviceName),

                22 => string.Format(ResourceString.GetString("diag_hw_analysis_22") ??
                    "Code 22 indicates '{0}' has been explicitly disabled at the OS or firmware level. " +
                    "The PnP manager has marked the device node as D3 (Off), and Windows is currently blocking all I/O traffic to this node. " +
                    "This can happen manually by a user, or dynamically by the OS to preserve stability.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to inject a deep PnP enable command to awaken the device.", deviceName),

                24 => string.Format(ResourceString.GetString("diag_hw_analysis_24") ??
                    "Code 24 indicates '{0}' is not present, is not working properly, or does not have all its drivers installed. " +
                    "This is often seen with 'ghosted' hardware—devices that have been physically unplugged from the machine but left a residual tracking trace in the registry. " +
                    "The OS is confused because the physical hardware is missing.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear the ghosted state and rebuild the hardware tree.", deviceName),

                28 => string.Format(ResourceString.GetString("diag_hw_analysis_28") ??
                    "Code 28 indicates a missing driver signature. '{0}' is physically connected, but Windows has no idea how to communicate with it because the INF driver package is missing from the Driver Store. " +
                    "The device will remain in a fallback 'Generic' state until proper instructions are provided.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to trigger a Plug-and-Play (PnP) rescan to force Windows to locate the driver.", deviceName),

                31 => string.Format(ResourceString.GetString("diag_hw_analysis_31") ??
                    "Code 31 means Windows recognizes '{0}', but the driver itself is broken, missing core dependencies, or incompatible with the current Windows kernel architecture. " +
                    "The driver attempted to load, but failed its internal dependency validation checks.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state and reload the dependencies.", deviceName),

                32 => string.Format(ResourceString.GetString("diag_hw_analysis_32") ??
                    "Code 32 means a background service associated with '{0}' has been disabled in the Service Control Manager. " +
                    "The hardware relies on a user-mode or kernel-mode service to function, and that service cannot be started. " +
                    "An alternate generic driver may be providing limited functionality.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to restore default startup states in the Service Control Manager.", deviceName),

                37 => string.Format(ResourceString.GetString("diag_hw_analysis_37") ??
                    "Code 37 indicates Windows cannot initialize the device driver for '{0}'. " +
                    "The driver passed the OS signature check but failed during its execution of the `DriverEntry` routine. " +
                    "This usually points to a fatal logic error inside the driver code itself.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle.", deviceName),

                38 => string.Format(ResourceString.GetString("diag_hw_analysis_38") ??
                    "Code 38 means Windows cannot load the driver for '{0}' because a previous, crashed instance of the driver is still stuck in active memory. " +
                    "The OS refuses to load a second instance to prevent memory corruption or blue screens.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a deep device power cycle and flush the stuck memory block.", deviceName),

                39 => string.Format(ResourceString.GetString("diag_hw_analysis_39") ??
                    "Code 39 is a severe driver corruption error. The .SYS driver file for '{0}' is either physically missing from System32, corrupted by bad disk sectors, or infected. " +
                    "Windows aborts the load process to protect the kernel from executing arbitrary or damaged code.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force Windows to drop the corrupt driver from memory and rebuild the connection.", deviceName),

                41 => string.Format(ResourceString.GetString("diag_hw_analysis_41") ??
                    "Code 41 means Windows successfully loaded the driver for '{0}' but cannot locate the physical hardware device on the motherboard bus. " +
                    "This usually happens when a device is rapidly unplugged while the OS is actively booting it up.\n\n" +
                    "RECOMMENDATION: Check physical connections, reseat the hardware, and click 'Fix' to rescan the bus.", deviceName),

                43 => string.Format(ResourceString.GetString("diag_hw_analysis_43") ??
                    "Code 43 is a hardware protection halt. Windows actively stopped '{0}' because the hardware reported a catastrophic problem or physical malfunction. " +
                    "This is extremely common with overheating GPUs, damaged USB hubs, or failing firmware logic. The OS isolates the hardware to prevent a full system crash.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to perform a deep power-cycle and attempt to clear the hardware fault state.", deviceName),

                45 => string.Format(ResourceString.GetString("diag_hw_analysis_45") ??
                    "Code 45 indicates a 'Ghosted' device. Windows remembers '{0}' from a previous session, but it is currently disconnected from the motherboard. " +
                    "This error also appears if a device connection is extremely loose and intermittently dropping voltage.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to re-initialize the bus and clear the ghosted state.", deviceName),

                47 => string.Format(ResourceString.GetString("diag_hw_analysis_47") ??
                    "Code 47 indicates '{0}' has been successfully prepared for safe removal (e.g., clicking 'Safely Remove Hardware'), but it has not yet been physically unplugged from the machine. " +
                    "The OS has halted all I/O to the port.\n\n" +
                    "RECOMMENDATION: Physically unplug the device, or restart the system.", deviceName),

                48 => string.Format(ResourceString.GetString("diag_hw_analysis_48") ??
                    "Code 48 means the software for '{0}' has been actively blocked from starting by Windows. " +
                    "This is usually due to a severe security risk, a revoked digital certificate, or a strict enterprise AppLocker/Device Guard policy.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully uninstall the restricted device node.", deviceName),

                49 => string.Format(ResourceString.GetString("diag_hw_analysis_49") ??
                    "Code 49 indicates Windows cannot start '{0}' because the system registry hive has exceeded its physical size limits. " +
                    "The OS cannot allocate enough paged pool memory to store the complex configuration data for this device.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to clear orphaned registry devices and compress the system hive.", deviceName),

                50 => string.Format(ResourceString.GetString("diag_hw_analysis_50") ??
                    "Code 50 means Windows cannot apply all of the properties for '{0}'. " +
                    "Device properties include vital capability descriptors and power settings. Without these, the device operates in a degraded fallback mode.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a strict hardware tree rescan.", deviceName),

                51 => string.Format(ResourceString.GetString("diag_hw_analysis_51") ??
                    "Code 51 indicates '{0}' is currently waiting on a parent device or physical bus bridge to start before it can initialize. " +
                    "This is a dependency chain halt—the hardware cannot power on until its upstream controller is healthy.\n\n" +
                    "RECOMMENDATION: Investigate the parent controller in the Device Manager tree.", deviceName),

                52 => string.Format(ResourceString.GetString("diag_hw_analysis_52") ??
                    "Code 52 is a Secure Boot / Driver Signature Enforcement block. The driver for '{0}' is unsigned or its digital signature has been altered. " +
                    "Windows Kernel Mode Code Integrity (KMCI) is blocking the driver to protect the OS from rootkits and malware.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to cleanly rebuild the registry parameters and attempt to unblock the signature cache.", deviceName),

                53 => string.Format(ResourceString.GetString("diag_hw_analysis_53") ??
                    "Code 53 indicates '{0}' is a boot device, but it has been restricted by Windows Kernel DMA Protection or an enterprise security policy. " +
                    "The OS is preventing Direct Memory Access to secure the boot process.\n\n" +
                    "RECOMMENDATION: Verify firmware security settings.", deviceName),

                54 => string.Format(ResourceString.GetString("diag_hw_analysis_54") ??
                    "Code 54 means '{0}' has failed and is currently undergoing a soft-reset by the OS bus driver. " +
                    "The system is automatically attempting to recover the hardware state without requiring a full reboot.\n\n" +
                    "RECOMMENDATION: Wait a few seconds for the device to recover, or restart the system.", deviceName),

                // ACPI & ADVANCED POWER MANAGEMENT (81 - 105)

                (>= 81 and <= 82) => string.Format(ResourceString.GetString("diag_hw_analysis_81") ??
                    "ACPI S1 Sleep State Rejection. The firmware for '{0}' refused a transition into the S1 sleep state (CPU stopped, RAM refreshed). " +
                    "The OS requested a low-latency power-down, but the device driver is holding a pending I/O Request Packet (IRP) that cannot be safely cancelled. " +
                    "This often results in the system refusing to sleep or waking up immediately after sleeping.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle and clear pending IRPs.", deviceName),

                (>= 83 and <= 84) => string.Format(ResourceString.GetString("diag_hw_analysis_83") ??
                    "ACPI S2 Sleep State Desynchronization. '{0}' failed to power down its internal clocks during an S2 transition. " +
                    "The device is consuming excess power and preventing the motherboard from achieving C-State residency.\n\n" +
                    "RECOMMENDATION: Update the motherboard chipset drivers.", deviceName),

                (>= 85 and <= 86) => string.Format(ResourceString.GetString("diag_hw_analysis_85") ??
                    "ACPI S3 Suspend-to-RAM Failure. '{0}' failed to save its hardware context to RAM during a sleep event. " +
                    "When the system attempts to wake up, this device will likely be unresponsive or cause a bugcheck (BSOD) because its memory state was lost.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device's ACPI power management registers.", deviceName),

                (>= 87 and <= 88) => string.Format(ResourceString.GetString("diag_hw_analysis_87") ??
                    "ACPI S3 Wake Interrupt Drop. The motherboard attempted to wake '{0}' from an S3 sleep state, but the hardware failed to assert its wake interrupt. " +
                    "The device remains in a zombie state (powered but unresponsive).\n\n" +
                    "RECOMMENDATION: Disable 'Allow the computer to turn off this device to save power' in Device Manager.", deviceName),

                (>= 89 and <= 90) => string.Format(ResourceString.GetString("diag_hw_analysis_89") ??
                    "ACPI D-State (Device State) Transition Timeout. '{0}' was commanded to transition from D0 (Fully On) to D3 (Off), but the firmware stalled. " +
                    "This usually indicates a severe logic error within the hardware's internal microcontroller.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a hardware reset via the PCIe or USB bus.", deviceName),

                (>= 91 and <= 92) => string.Format(ResourceString.GetString("diag_hw_analysis_91") ??
                    "ACPI S4 Hibernation Block. The driver for '{0}' failed to write its state to the hiberfil.sys file. " +
                    "The device is actively preventing the system from safely entering deep hibernation to prevent data corruption.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the fast startup and hibernation configurations.", deviceName),

                (>= 93 and <= 94) => string.Format(ResourceString.GetString("diag_hw_analysis_93") ??
                    "ACPI S4 Wake Context Restoration Failure. The system resumed from hibernation, but '{0}' could not parse the restored data block. " +
                    "The device driver is currently executing with corrupted internal variables.\n\n" +
                    "RECOMMENDATION: Restart the machine to flush the corrupted driver context.", deviceName),

                (>= 95 and <= 97) => string.Format(ResourceString.GetString("diag_hw_analysis_95") ??
                    "ACPI S0ix Modern Standby Anomaly. '{0}' failed to enter a low-power idle state (Modern Standby). " +
                    "This keeps the CPU active, leading to severe battery drain and overheating while the laptop lid is closed.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to invalidate the current driver power state and force S0ix compliance.", deviceName),

                (>= 98 and <= 100) => string.Format(ResourceString.GetString("diag_hw_analysis_98") ??
                    "ACPI Connected Standby Network Drop. '{0}' is required to maintain a low-power network connection during sleep, but the radio firmware crashed. " +
                    "Background tasks (like email syncing) will fail while the device is asleep.\n\n" +
                    "RECOMMENDATION: Update the Wi-Fi or Cellular modem firmware.", deviceName),

                (>= 101 and <= 103) => string.Format(ResourceString.GetString("diag_hw_analysis_101") ??
                    "ACPI Wake Alarm Failure. '{0}' generated a spurious wake event, bringing the system out of sleep unexpectedly. " +
                    "This is typically caused by overly sensitive network adapters (Magic Packet wake) or failing USB hubs.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to disable wake-on-LAN and external wake triggers for this hardware.", deviceName),

                (>= 104 and <= 105) => string.Format(ResourceString.GetString("diag_hw_analysis_104") ??
                    "ACPI Thermal Zone Interface Drop. '{0}' failed to report its thermal state to the OS power manager. " +
                    "The OS may forcefully spin up fans to 100% as a fail-safe measure.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the thermal polling interval.", deviceName),

                // I/O & MEMORY MAPPING (106 - 135)

                (>= 106 and <= 108) => string.Format(ResourceString.GetString("diag_hw_analysis_106") ??
                    "MMIO 32-bit Memory Mapping Failure. Windows cannot assign the required physical memory blocks to '{0}'. " +
                    "The 32-bit Memory-Mapped I/O space (below 4GB) is completely exhausted, usually due to a BIOS configuration error or too many PCIe expansion cards.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force the PnP manager to re-evaluate the motherboard resource pool.", deviceName),

                (>= 109 and <= 112) => string.Format(ResourceString.GetString("diag_hw_analysis_109") ??
                    "MMIO Resource Overlap. '{0}' is attempting to claim a memory address block that is already owned by the motherboard chipset or another device. " +
                    "This creates an unresolvable hardware conflict.\n\n" +
                    "RECOMMENDATION: Move the physical expansion card to a different PCIe slot.", deviceName),

                (>= 113 and <= 116) => string.Format(ResourceString.GetString("diag_hw_analysis_113") ??
                    "MMIO 64-bit (Above 4G Decoding) Fault. '{0}' requires a massive memory address space (typical for high-end GPUs or accelerators), but Above 4G Decoding is disabled. " +
                    "The OS cannot fit the required VRAM map into the legacy 32-bit space.\n\n" +
                    "RECOMMENDATION: Enable 'Above 4G Decoding' or 'Resizable BAR' in your motherboard BIOS.", deviceName),

                (>= 117 and <= 120) => string.Format(ResourceString.GetString("diag_hw_analysis_117") ??
                    "Resizable BAR (Base Address Register) Negotiation Failure. The motherboard and '{0}' failed to agree on a dynamic memory allocation size. " +
                    "The device has fallen back to a highly restrictive 256MB memory map, crippling performance.\n\n" +
                    "RECOMMENDATION: Update your motherboard BIOS and GPU firmware.", deviceName),

                (>= 121 and <= 124) => string.Format(ResourceString.GetString("diag_hw_analysis_121") ??
                    "PCI Configuration Space Exhaustion. '{0}' failed to register its Base Address Registers (BARs) during the early boot phase. " +
                    "The motherboard cannot allocate the fundamental I/O addresses needed for the CPU to talk to the hardware.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state.", deviceName),

                (>= 125 and <= 128) => string.Format(ResourceString.GetString("diag_hw_analysis_125") ??
                    "PCI Extended Configuration Space Error. The OS attempted to read the advanced capabilities of '{0}' beyond the standard 256-byte limit, but the bus returned an error. " +
                    "This indicates a severe communication breakdown on the PCIe bus.\n\n" +
                    "RECOMMENDATION: Reseat the hardware or clean the PCIe gold contacts.", deviceName),

                (>= 129 and <= 132) => string.Format(ResourceString.GetString("diag_hw_analysis_129") ??
                    "Port I/O Space Conflict. Legacy hardware port addresses (e.g., 0x3F8) for '{0}' are currently locked by another legacy device. " +
                    "The system cannot arbitrate the 16-bit address space.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force Windows to reallocate legacy port vectors.", deviceName),

                (>= 133 and <= 135) => string.Format(ResourceString.GetString("diag_hw_analysis_133") ??
                    "Direct Port Access Violation. A user-mode application attempted to directly access the hardware registers of '{0}' without going through the kernel driver. " +
                    "Windows has blocked the access to prevent a security compromise.\n\n" +
                    "RECOMMENDATION: Close legacy hardware monitoring tools that rely on direct I/O.", deviceName),

                // PCI EXPRESS (PCIe) SUBSYSTEM (136 - 155)

                (>= 136 and <= 138) => string.Format(ResourceString.GetString("diag_hw_analysis_136") ??
                    "PCIe ASPM (Active State Power Management) L0s Failure. The PCIe link for '{0}' dropped connection while attempting to enter a standby state. " +
                    "The L0s link state transitioned incorrectly, causing the hardware to briefly disappear from the bus.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to disable ASPM for this specific PCIe root port.", deviceName),

                (>= 139 and <= 140) => string.Format(ResourceString.GetString("diag_hw_analysis_139") ??
                    "PCIe ASPM L1 Exit Latency Exceeded. '{0}' was in a deep sleep state (L1) and took too long to wake up when the CPU requested data. " +
                    "This causes extreme micro-stuttering in high-performance applications.\n\n" +
                    "RECOMMENDATION: Change the Windows Power Plan to 'High Performance' to disable PCIe Link State Power Management.", deviceName),

                (>= 141 and <= 143) => string.Format(ResourceString.GetString("diag_hw_analysis_141") ??
                    "PCIe Link Training Error. The motherboard negotiated a slower connection speed (e.g., Gen4 downgraded to Gen1) for '{0}' due to signal integrity loss. " +
                    "This severely impacts bandwidth and performance.\n\n" +
                    "RECOMMENDATION: Reseat the device in its PCIe slot or verify the integrity of the PCIe riser cable.", deviceName),

                (>= 144 and <= 145) => string.Format(ResourceString.GetString("diag_hw_analysis_144") ??
                    "PCIe Lane Width Downgrade. '{0}' is requesting x16 lanes of bandwidth but the motherboard is only providing x8 or x4. " +
                    "This is usually caused by populating an M.2 slot that shares lanes with the primary GPU slot.\n\n" +
                    "RECOMMENDATION: Check your motherboard manual for PCIe lane sharing rules.", deviceName),

                (>= 146 and <= 148) => string.Format(ResourceString.GetString("diag_hw_analysis_146") ??
                    "PCIe Advanced Error Reporting (AER) Correctable Error. '{0}' threw a corrected hardware error on the PCI Express bus (e.g., a Bad TLP or Receiver Error). " +
                    "The hardware successfully recovered, but the signal quality is marginal.\n\n" +
                    "RECOMMENDATION: Monitor system stability. If crashes occur, the hardware may be failing.", deviceName),

                (>= 149 and <= 150) => string.Format(ResourceString.GetString("diag_hw_analysis_149") ??
                    "PCIe AER Uncorrectable Error (Fatal). '{0}' generated a Malformed TLP or Poisoned Data packet. " +
                    "The OS has halted the device to prevent memory corruption.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a hard reset on the PCIe root complex.", deviceName),

                (>= 151 and <= 153) => string.Format(ResourceString.GetString("diag_hw_analysis_151") ??
                    "PCIe Lane Clock Desynchronization. The reference clock (BCLK) for '{0}' drifted out of tolerance. " +
                    "The CPU and the device are operating out of sync, leading to dropped packets and extreme DPC latency.\n\n" +
                    "RECOMMENDATION: Disable extreme BCLK or PCIe overclocks in your BIOS.", deviceName),

                (>= 154 and <= 155) => string.Format(ResourceString.GetString("diag_hw_analysis_154") ??
                    "PCIe Spread Spectrum Clocking (SSC) Mismatch. The motherboard and '{0}' cannot synchronize their EMI-reduction clocking signals. \n\n" +
                    "RECOMMENDATION: Disable Spread Spectrum in the motherboard BIOS.", deviceName),

                // INTERRUPT HANDLING (IRQ, MSI, MSI-X) (156 - 185)

                (>= 156 and <= 159) => string.Format(ResourceString.GetString("diag_hw_analysis_156") ??
                    "Line-Based IRQ Conflict. '{0}' is attempting to share a legacy hardware interrupt line with a high-priority device. " +
                    "Because they share the same physical trace, the CPU wastes cycles asking which device triggered the interrupt.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to flush ghosted devices and force Windows to reallocate legacy interrupt lines.", deviceName),

                (>= 160 and <= 162) => string.Format(ResourceString.GetString("diag_hw_analysis_160") ??
                    "IRQ Routing Table Exhaustion. The ACPI routing tables provided by the BIOS do not have enough IRQ vectors for '{0}'. " +
                    "The OS cannot route the physical interrupt to a logical processor.\n\n" +
                    "RECOMMENDATION: Update the motherboard BIOS to fix ACPI table generation.", deviceName),

                (>= 163 and <= 166) => string.Format(ResourceString.GetString("diag_hw_analysis_163") ??
                    "Message Signaled Interrupts (MSI) Mapping Fault. '{0}' failed to allocate an MSI vector. " +
                    "The APIC (Advanced Programmable Interrupt Controller) rejected the device's memory-write interrupt request.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force Windows to reallocate MSI vectors.", deviceName),

                (>= 167 and <= 170) => string.Format(ResourceString.GetString("diag_hw_analysis_167") ??
                    "MSI Vector Limit Reached. '{0}' requested multiple MSI vectors to distribute load across CPU cores, but the OS limited it to a single vector. " +
                    "Performance will be bottlenecked to a single logical processor.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the interrupt affinity policy in the registry.", deviceName),

                (>= 171 and <= 174) => string.Format(ResourceString.GetString("diag_hw_analysis_171") ??
                    "MSI-X Vector Table Exhaustion. '{0}' requested more MSI-X hardware queues than the motherboard or OS can currently provide. " +
                    "This usually affects high-end NVMe drives or 10Gbps+ Network Adapters designed for highly threaded workloads.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device and force a fallback to standard MSI mode.", deviceName),

                (>= 175 and <= 178) => string.Format(ResourceString.GetString("diag_hw_analysis_175") ??
                    "MSI-X PBA (Pending Bit Array) Lock. The interrupt controller for '{0}' is masking interrupts indefinitely, causing the driver to starve for data.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle.", deviceName),

                (>= 179 and <= 182) => string.Format(ResourceString.GetString("diag_hw_analysis_179") ??
                    "Interrupt (IRQ) Storm Detection. '{0}' is spamming the CPU with thousands of unnecessary hardware interrupts per second. " +
                    "The Windows kernel has temporarily suppressed the device to prevent a total system lockup (100% CPU usage on a single core).\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle and clear the interrupt queue.", deviceName),

                (>= 183 and <= 185) => string.Format(ResourceString.GetString("diag_hw_analysis_183") ??
                    "Deferred Procedure Call (DPC) Watchdog Timeout. The driver for '{0}' spent too much time executing at DISPATCH_LEVEL. " +
                    "This causes audio crackling, mouse stuttering, and eventual system bugchecks (DPC_WATCHDOG_VIOLATION).\n\n" +
                    "RECOMMENDATION: Roll back the driver for this device to a previously stable version.", deviceName),

                // USB, THUNDERBOLT, & SERIAL BUSES (186 - 268)

                (>= 186 and <= 188) => string.Format(ResourceString.GetString("diag_hw_analysis_186") ??
                    "USB Configuration Descriptor Parse Fault. '{0}' was plugged in, but Windows could not read its fundamental hardware ID. " +
                    "The USB device returned garbage data during the initial enumeration handshake.\n\n" +
                    "RECOMMENDATION: The USB device may be physically damaged. Click 'Fix' to reset the USB Root Hub.", deviceName),

                (>= 189 and <= 190) => string.Format(ResourceString.GetString("diag_hw_analysis_189") ??
                    "USB String Descriptor Timeout. '{0}' responded to the host controller but took too long to provide its Manufacturer and Product name strings. " +
                    "The device has been placed in an error state.\n\n" +
                    "RECOMMENDATION: Unplug the device and try a different USB port, preferably directly on the motherboard.", deviceName),

                (>= 191 and <= 193) => string.Format(ResourceString.GetString("diag_hw_analysis_191") ??
                    "USB VBUS Power Surge / Overcurrent. '{0}' drew more electrical current than the USB port can safely supply. " +
                    "The motherboard has actively shut down the port to prevent electrical damage to the system.\n\n" +
                    "RECOMMENDATION: Unplug the device immediately. Avoid using unpowered USB splitters.", deviceName),

                (>= 194 and <= 195) => string.Format(ResourceString.GetString("diag_hw_analysis_194") ??
                    "USB Selective Suspend Failure. The USB hub serving '{0}' attempted to drop power to save battery, but the device refused the command and locked the bus.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to disable USB Selective Suspend in the Windows Power Plan.", deviceName),

                (>= 196 and <= 198) => string.Format(ResourceString.GetString("diag_hw_analysis_196") ??
                    "USB Isochronous Bandwidth Exhaustion. '{0}' requested more real-time USB bandwidth than the host controller has available. " +
                    "This commonly happens when too many webcams, audio interfaces, or capture cards share a single USB controller.\n\n" +
                    "RECOMMENDATION: Move the device to a completely different USB port bank on the motherboard.", deviceName),

                (>= 199 and <= 200) => string.Format(ResourceString.GetString("diag_hw_analysis_199") ??
                    "USB Bulk Endpoint Stall. A massive data transfer to or from '{0}' (usually a hard drive or flash drive) encountered a physical bus error and stalled.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to send a Clear Feature command to the USB endpoint.", deviceName),

                (>= 201 and <= 203) => string.Format(ResourceString.GetString("diag_hw_analysis_201") ??
                    "USB Set Address Failure. The USB host controller successfully detected '{0}', but the device timed out when the OS assigned it a unique USB address. " +
                    "The enumeration process has completely failed.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a strict hardware tree rescan of the USB topology.", deviceName),

                (>= 204 and <= 205) => string.Format(ResourceString.GetString("diag_hw_analysis_204") ??
                    "USB SuperSpeed (USB 3.x) Fallback. '{0}' is a high-speed device but failed to negotiate SuperSpeed signaling. " +
                    "It is currently operating in a severely degraded USB 2.0 legacy mode.\n\n" +
                    "RECOMMENDATION: Check for dust in the USB port or use a higher quality cable.", deviceName),

                (>= 206 and <= 209) => string.Format(ResourceString.GetString("diag_hw_analysis_206") ??
                    "Thunderbolt / USB4 Security Block. '{0}' was detected on the PCIe tunnel, but the system's Thunderbolt Security Level is actively blocking it. " +
                    "Direct Memory Access is restricted until user authorization is provided.\n\n" +
                    "RECOMMENDATION: Approve the device in the Thunderbolt Control Center.", deviceName),

                (>= 210 and <= 212) => string.Format(ResourceString.GetString("diag_hw_analysis_210") ??
                    "Thunderbolt PCIe Tunneling Rejection. The host controller refused to establish a PCIe tunnel for '{0}' due to bandwidth limitations on the root port.\n\n" +
                    "RECOMMENDATION: Disconnect other heavy bandwidth devices (like eGPUs) from the Thunderbolt chain.", deviceName),

                (>= 213 and <= 216) => string.Format(ResourceString.GetString("diag_hw_analysis_213") ??
                    "Thunderbolt DisplayPort Alt-Mode Failure. The high-speed bus connecting '{0}' failed to negotiate a video signal lane. " +
                    "The cable may be damaged or lacks the necessary bandwidth for the display resolution.\n\n" +
                    "RECOMMENDATION: Swap the USB-C / Thunderbolt cable for an active, certified 40Gbps cable.", deviceName),

                (>= 217 and <= 219) => string.Format(ResourceString.GetString("diag_hw_analysis_217") ??
                    "Thunderbolt Power Delivery (PD) Negotiation Drop. '{0}' requested a specific voltage/amperage profile, but the host controller cannot supply it.\n\n" +
                    "RECOMMENDATION: Connect external power to the Thunderbolt dock or peripheral.", deviceName),

                (>= 220 and <= 223) => string.Format(ResourceString.GetString("diag_hw_analysis_220") ??
                    "Thunderbolt Daisy-Chain Depth Limit. '{0}' exceeds the maximum allowed topological depth for a Thunderbolt chain (usually 6 devices). " +
                    "The latency is too high for the OS to guarantee stable operation.\n\n" +
                    "RECOMMENDATION: Connect the device closer to the host system port.", deviceName),

                (>= 224 and <= 226) => string.Format(ResourceString.GetString("diag_hw_analysis_224") ??
                    "Thunderbolt Hot-Plug Event Storm. '{0}' is rapidly asserting and de-asserting its presence on the bus, causing the OS enumerator to thrash.\n\n" +
                    "RECOMMENDATION: The cable or port is physically failing. Replace the hardware.", deviceName),

                (>= 227 and <= 231) => string.Format(ResourceString.GetString("diag_hw_analysis_227") ??
                    "USB4 NVM Firmware Auth Failure. The internal firmware on '{0}' failed a cryptographic validation check during handshake. " +
                    "The host controller has sandboxed the device.\n\n" +
                    "RECOMMENDATION: Update the firmware of your Thunderbolt/USB4 dock or peripheral.", deviceName),

                (>= 232 and <= 235) => string.Format(ResourceString.GetString("diag_hw_analysis_232") ??
                    "USB4 Router Configuration Error. The connection manager failed to assign a unique router ID to '{0}'.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reboot the Thunderbolt baseband controller.", deviceName),

                (>= 236 and <= 240) => string.Format(ResourceString.GetString("diag_hw_analysis_236") ??
                    "I2C Clock Stretching Timeout. The communication line to '{0}' (touchpad, sensor, or biometric device) was held low for too long. " +
                    "The physical bus timed out waiting for the slow device to respond.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reboot the local I2C controller and restore peripheral input.", deviceName),

                (>= 241 and <= 245) => string.Format(ResourceString.GetString("diag_hw_analysis_241") ??
                    "I2C NACK (Not Acknowledge) Received. The host addressed '{0}' on the I2C bus, but the device did not respond. " +
                    "The hardware may be in a deep sleep state or physically disconnected internally.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle.", deviceName),

                (>= 246 and <= 250) => string.Format(ResourceString.GetString("diag_hw_analysis_246") ??
                    "SPI Bus Arbitrary Transfer Drop. The high-speed serial interface communicating with '{0}' lost a packet during a full-duplex transfer. " +
                    "Input data (like a fingerprint read or precise keyboard stroke) was corrupted.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the SPI hardware connection state.", deviceName),

                (>= 251 and <= 255) => string.Format(ResourceString.GetString("diag_hw_analysis_251") ??
                    "SPI Clock Polarity Mismatch. The master controller and '{0}' are out of sync regarding clock edge reading, causing garbage data to be ingested by the OS.\n\n" +
                    "RECOMMENDATION: Update the chipset and serial I/O drivers.", deviceName),

                (>= 256 and <= 261) => string.Format(ResourceString.GetString("diag_hw_analysis_256") ??
                    "HID Descriptor Invalid. The Human Interface Device '{0}' sent a malformed report descriptor to Windows. " +
                    "The OS does not know how to interpret the input data. This often results in 'ghost touches', stuck keys, or erratic mouse movements.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully invalidate the driver state and reset the HID subsystem.", deviceName),

                (>= 262 and <= 268) => string.Format(ResourceString.GetString("diag_hw_analysis_262") ??
                    "HID Report Queue Overflow. '{0}' is generating input events faster than the Windows input stack can process them (e.g., an 8000Hz polling rate mouse). " +
                    "Input latency will spike drastically.\n\n" +
                    "RECOMMENDATION: Lower the polling rate in the device's companion software.", deviceName),

                // BLUETOOTH, WI-FI, & HDCP/DRM (269 - 335)

                (>= 269 and <= 274) => string.Format(ResourceString.GetString("diag_hw_analysis_269") ??
                    "Bluetooth Baseband Core Dump. The local Bluetooth radio handling '{0}' experienced a fatal firmware crash. " +
                    "The entire Bluetooth stack has halted and requires a hardware reset.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle on the RF radio module.", deviceName),

                (>= 275 and <= 279) => string.Format(ResourceString.GetString("diag_hw_analysis_275") ??
                    "Bluetooth HCI (Host Controller Interface) Command Timeout. The Windows driver sent a command to '{0}', but the Bluetooth radio chip failed to execute it.\n\n" +
                    "RECOMMENDATION: Toggle Bluetooth off and on in Windows settings.", deviceName),

                (>= 280 and <= 285) => string.Format(ResourceString.GetString("diag_hw_analysis_280") ??
                    "Bluetooth LMP (Logical Link Control) Timeout. The wireless connection to '{0}' dropped because the device failed to acknowledge keep-alive packets. " +
                    "The device has moved out of range or run out of battery.\n\n" +
                    "RECOMMENDATION: Ensure the device is fully charged and within RF range.", deviceName),

                (>= 286 and <= 290) => string.Format(ResourceString.GetString("diag_hw_analysis_286") ??
                    "Bluetooth Simple Pairing (SSP) Rejection. The pairing sequence for '{0}' failed due to a mismatched PIN or an invalid public key exchange.\n\n" +
                    "RECOMMENDATION: Remove the device from Windows settings and re-pair it.", deviceName),

                (>= 291 and <= 295) => string.Format(ResourceString.GetString("diag_hw_analysis_291") ??
                    "Wi-Fi / Bluetooth Coexistence Interference. The 2.4GHz spectrum is overly saturated, causing the antenna for '{0}' to drop packets to preserve Wi-Fi throughput. " +
                    "Bluetooth audio will stutter severely.\n\n" +
                    "RECOMMENDATION: Switch your primary Wi-Fi network to the 5GHz or 6GHz band.", deviceName),

                (>= 296 and <= 301) => string.Format(ResourceString.GetString("diag_hw_analysis_296") ??
                    "RF Antenna Diversity Switch Failure. The module for '{0}' failed to switch between its internal antennas, leading to a massive drop in signal quality.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state.", deviceName),

                (>= 302 and <= 310) => string.Format(ResourceString.GetString("diag_hw_analysis_302") ??
                    "HDCP 1.4/2.2 Handshake Auth Failure. Secure display routing for '{0}' failed a cryptographic handshake. " +
                    "Protected content (like Netflix or Blu-Rays) will display as a black screen, snow, or drop in resolution.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to cleanly rebuild the registry parameters and flush the DRM cache.", deviceName),

                (>= 311 and <= 316) => string.Format(ResourceString.GetString("diag_hw_analysis_311") ??
                    "HDCP Link Integrity Failure. The encryption keys for '{0}' were successfully exchanged, but the physical link dropped enough frames to trigger a security revocation.\n\n" +
                    "RECOMMENDATION: Use a shorter, higher-quality HDMI or DisplayPort cable.", deviceName),

                (>= 317 and <= 322) => string.Format(ResourceString.GetString("diag_hw_analysis_317") ??
                    "DRM PlayReady Secure Environment Drop. The Trusted Execution Environment (TEE) required by '{0}' collapsed during secure video playback. " +
                    "The hardware DRM enclave has crashed.\n\n" +
                    "RECOMMENDATION: Update your graphics drivers to restore secure memory enclaves.", deviceName),

                (>= 323 and <= 330) => string.Format(ResourceString.GetString("diag_hw_analysis_323") ??
                    "Audio Protected Path (PE-Auth) Violation. The secure audio stream directed to '{0}' was intercepted or altered by an unauthorized audio filter. " +
                    "Windows has muted the stream to prevent unauthorized recording of DRM content.\n\n" +
                    "RECOMMENDATION: Disable third-party audio enhancement software (e.g., Nahimic, Waves MaxxAudio).", deviceName),

                (>= 331 and <= 335) => string.Format(ResourceString.GetString("diag_hw_analysis_331") ??
                    "Secure Audio Output Content Protection (OPM) Revocation. The driver for '{0}' was flagged as compromised and is no longer allowed to process DRM audio.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully reinstall the audio driver.", deviceName),

                // DMA, NETWORKING & AUDIO (336 - 502)

                (>= 336 and <= 342) => string.Format(ResourceString.GetString("diag_hw_analysis_336") ??
                    "IOMMU / VT-d Page Walk Error. The hardware hypervisor failed to translate a virtual memory address for '{0}'. " +
                    "The device attempted to access memory outside of its isolated domain, triggering a hardware protection fault.\n\n" +
                    "RECOMMENDATION: Check for motherboard BIOS updates relating to VT-d or AMD-Vi.", deviceName),

                (>= 343 and <= 350) => string.Format(ResourceString.GetString("diag_hw_analysis_343") ??
                    "IOMMU Device Context Entry (DCE) Missing. The motherboard failed to assign an isolation domain for '{0}'. " +
                    "The OS refuses to trust the device.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state.", deviceName),

                (>= 351 and <= 360) => string.Format(ResourceString.GetString("diag_hw_analysis_351") ??
                    "Kernel DMA Protection Block. Windows isolated '{0}' from system memory because it attempted an unauthorized Direct Memory Access operation. " +
                    "This is a core security feature designed to prevent Thunderbolt/PCIe DMA attacks (e.g., PCILeech).\n\n" +
                    "RECOMMENDATION: Click 'Fix' to safely invalidate the driver state and reset the hardware.", deviceName),

                (>= 361 and <= 370) => string.Format(ResourceString.GetString("diag_hw_analysis_361") ??
                    "DMA Remapping Failure. The OS attempted to map a secure memory buffer for '{0}', but the memory ranges were highly fragmented. " +
                    "The device cannot process large file transfers.\n\n" +
                    "RECOMMENDATION: Restart the system to defragment physical memory.", deviceName),

                (>= 371 and <= 380) => string.Format(ResourceString.GetString("diag_hw_analysis_371") ??
                    "Scatter-Gather List (SGL) Corruption. The memory mapping table used by '{0}' to access RAM became fragmented or corrupted. " +
                    "Data read from or written to this device will be heavily corrupted.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle.", deviceName),

                (>= 381 and <= 390) => string.Format(ResourceString.GetString("diag_hw_analysis_381") ??
                    "Map Register Exhaustion. '{0}' requested too many hardware map registers for a DMA transfer. " +
                    "The motherboard chipset has run out of resources to route the data.\n\n" +
                    "RECOMMENDATION: Avoid overloading legacy SATA/USB controllers with massive concurrent transfers.", deviceName),

                (>= 391 and <= 400) => string.Format(ResourceString.GetString("diag_hw_analysis_391") ??
                    "Direct Memory Access (DMA) Channel Collision. '{0}' tried to hijack a legacy DMA channel already in use by another hardware component. " +
                    "This is an antiquated hardware conflict mostly seen on very old peripherals.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force Windows to reallocate DMA channels.", deviceName),

                (>= 401 and <= 410) => string.Format(ResourceString.GetString("diag_hw_analysis_401") ??
                    "Bus Master Abort. '{0}' initiated a DMA transfer but the motherboard bridge controller aborted the transaction due to a parity error. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the PCIe bridge controller.", deviceName),

                (>= 411 and <= 418) => string.Format(ResourceString.GetString("diag_hw_analysis_411") ??
                    "Network NIC Checksum Offload Fault. '{0}' failed to calculate the IPv4/TCP checksums in hardware, resulting in corrupted network packets. " +
                    "The remote server is actively dropping your traffic.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to disable hardware checksum offloading and force software calculation.", deviceName),

                (>= 419 and <= 425) => string.Format(ResourceString.GetString("diag_hw_analysis_419") ??
                    "IPsec Task Offload Failure. '{0}' failed to encrypt or decrypt an IPsec payload in hardware. " +
                    "Secure VPN connections will fail.\n\n" +
                    "RECOMMENDATION: Update your Network Interface Card (NIC) drivers.", deviceName),

                (>= 426 and <= 433) => string.Format(ResourceString.GetString("diag_hw_analysis_426") ??
                    "Network Large Send Offload (LSO) Segment Drop. '{0}' dropped a massive network frame while trying to split it into smaller MTU packets. " +
                    "This causes extreme upload bandwidth throttling.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to disable Large Send Offload on the network adapter.", deviceName),

                (>= 434 and <= 440) => string.Format(ResourceString.GetString("diag_hw_analysis_434") ??
                    "Receive Segment Coalescing (RSC) Error. '{0}' failed to combine incoming network packets, overwhelming the CPU with thousands of tiny interrupts.\n\n" +
                    "RECOMMENDATION: Update the Network Interface Card (NIC) firmware.", deviceName),

                (>= 441 and <= 448) => string.Format(ResourceString.GetString("diag_hw_analysis_441") ??
                    "Receive Side Scaling (RSS) Queue Starvation. '{0}' is failing to distribute incoming network traffic across multiple CPU cores, causing a single core to bottleneck. " +
                    "Network throughput will be severely capped.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state and rebuild RSS queues.", deviceName),

                (>= 449 and <= 455) => string.Format(ResourceString.GetString("diag_hw_analysis_449") ??
                    "Network Adapter Ring Buffer Exhaustion. The internal memory of '{0}' is overflowing because the OS is not reading network packets fast enough. \n\n" +
                    "RECOMMENDATION: Increase the 'Receive Buffers' in the advanced properties of your network adapter.", deviceName),

                (>= 456 and <= 463) => string.Format(ResourceString.GetString("diag_hw_analysis_456") ??
                    "MAC PHY Link Drop / Auto-Negotiation Failure. The physical copper or fiber link for '{0}' failed to negotiate a stable gigabit connection. " +
                    "The adapter has likely dropped to 100Mbps or 10Mbps half-duplex.\n\n" +
                    "RECOMMENDATION: Inspect the physical Ethernet cable, swap to Cat6, and check the switch port.", deviceName),

                (>= 464 and <= 470) => string.Format(ResourceString.GetString("diag_hw_analysis_464") ??
                    "Transceiver Module (SFP/SFP+) I2C Read Failure. The network adapter cannot read the temperature or laser power telemetry from the inserted transceiver on '{0}'.\n\n" +
                    "RECOMMENDATION: Reseat the SFP module.", deviceName),

                (>= 471 and <= 475) => string.Format(ResourceString.GetString("diag_hw_analysis_471") ??
                    "Audio DSP Stream Format Unsupported. The Digital Signal Processor for '{0}' was fed an audio sample rate or bit-depth it cannot physically process (e.g., 384kHz/32-bit). " +
                    "The audio stack has crashed.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the audio hardware to the default 48kHz/24-bit format.", deviceName),

                (>= 476 and <= 480) => string.Format(ResourceString.GetString("diag_hw_analysis_476") ??
                    "Audio Endpoint Builder Failure. Windows failed to generate a logical software endpoint for the physical ports on '{0}'. " +
                    "The hardware exists, but no speakers or microphones show up in Windows.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the Windows Audio Endpoint Builder service.", deviceName),

                (>= 481 and <= 485) => string.Format(ResourceString.GetString("diag_hw_analysis_481") ??
                    "Audio Processing Object (APO) Crash. A software audio enhancement tied to '{0}' encountered a fatal exception and took down the entire audio stack. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to bypass Audio Enhancements and clear stuck buffers.", deviceName),

                (>= 486 and <= 490) => string.Format(ResourceString.GetString("diag_hw_analysis_486") ??
                    "Spatial Audio (Dolby/DTS/Windows Sonic) Format Drop. '{0}' failed to initialize the virtual surround sound matrix. \n\n" +
                    "RECOMMENDATION: Disable Spatial Audio in the Windows sound properties.", deviceName),

                (>= 491 and <= 496) => string.Format(ResourceString.GetString("diag_hw_analysis_491") ??
                    "High Definition Audio (HDA) Codec Ring Buffer Stall. '{0}' failed to cycle its audio buffers, resulting in a continuous robotic buzzing sound or complete silence. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle on the audio controller.", deviceName),

                (>= 497 and <= 502) => string.Format(ResourceString.GetString("diag_hw_analysis_497") ??
                    "HDA Codec Jack Sensing Failure. '{0}' cannot detect when headphones or microphones are physically plugged into the 3.5mm jacks. \n\n" +
                    "RECOMMENDATION: Update your Realtek/motherboard audio drivers.", deviceName),

                // HYPER-V, TPM & STORAGE CONTROLLERS (503 - 1169)

                (>= 503 and <= 520) => string.Format(ResourceString.GetString("diag_hw_analysis_503") ??
                    "Hyper-V VMBus Channel Offer Timeout. The host operating system failed to negotiate a communication channel with the virtualized representation of '{0}'. " +
                    "The synthetic device is entirely offline within the guest OS.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a strict PnP manager re-enumeration of the virtual bus.", deviceName),

                (>= 521 and <= 535) => string.Format(ResourceString.GetString("diag_hw_analysis_521") ??
                    "Hyper-V VMBus GPADL Allocation Failure. The host failed to allocate a Guest Physical Address Descriptor List for '{0}'. " +
                    "The virtual device cannot map its memory into the guest OS.\n\n" +
                    "RECOMMENDATION: Ensure Dynamic Memory is configured properly in Hyper-V settings.", deviceName),

                (>= 536 and <= 550) => string.Format(ResourceString.GetString("diag_hw_analysis_536") ??
                    "Hyper-V Synthetic Device Ring Buffer Overflow. The guest OS sent too many commands to '{0}', overwhelming the host's queue. " +
                    "The host has dropped the packets.\n\n" +
                    "RECOMMENDATION: Increase resource allocation to the virtual machine.", deviceName),

                (>= 551 and <= 570) => string.Format(ResourceString.GetString("diag_hw_analysis_551") ??
                    "Hyper-V Synthetic Interrupt (SynIC) Delivery Failure. The host could not inject a virtual hardware interrupt into the guest CPU for '{0}'. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state.", deviceName),

                (>= 571 and <= 590) => string.Format(ResourceString.GetString("diag_hw_analysis_571") ??
                    "SR-IOV Virtual Function (VF) Mapping Fault. The physical hardware failed to map a direct PCIe lane into the virtual machine for '{0}'. " +
                    "The device has fallen back to a slow, emulated synthetic path.\n\n" +
                    "RECOMMENDATION: Ensure SR-IOV and IOMMU are explicitly enabled in the motherboard BIOS.", deviceName),

                (>= 591 and <= 610) => string.Format(ResourceString.GetString("diag_hw_analysis_591") ??
                    "SR-IOV Physical Function (PF) Driver Crash. The host driver responsible for splitting '{0}' into virtual functions encountered a fatal exception. \n\n" +
                    "RECOMMENDATION: Update the firmware and host driver for the SR-IOV capable hardware.", deviceName),

                (>= 611 and <= 630) => string.Format(ResourceString.GetString("diag_hw_analysis_611") ??
                    "Host-to-Guest Integration Component Mismatch. The firmware protocol for '{0}' does not match the version expected by the hypervisor. " +
                    "Data exchange services (like copy/paste or time sync) are broken.\n\n" +
                    "RECOMMENDATION: Update Hyper-V Integration Services in the guest operating system.", deviceName),

                (>= 631 and <= 669) => string.Format(ResourceString.GetString("diag_hw_analysis_631") ??
                    "Hyper-V Virtual Machine Queue (VMQ) Allocation Error. The host cannot allocate hardware queues for '{0}' to accelerate network traffic. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the virtual network adapter.", deviceName),

                (>= 670 and <= 685) => string.Format(ResourceString.GetString("diag_hw_analysis_670") ??
                    "TPM 2.0 PCR Validation Mismatch. The Platform Configuration Registers for '{0}' changed unexpectedly. " +
                    "This indicates a potential boot-sector compromise, a recent BIOS update, or a hardware change. BitLocker will likely prompt for a recovery key.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force the OS to re-measure the hardware and update the TPM baseline.", deviceName),

                (>= 686 and <= 700) => string.Format(ResourceString.GetString("diag_hw_analysis_686") ??
                    "TPM Endorsement Key (EK) Certificate Parse Error. Windows cannot cryptographically verify the manufacturing origin of '{0}'. " +
                    "Windows Hello and secure attestation services will fail.\n\n" +
                    "RECOMMENDATION: Clear the TPM in the BIOS and re-provision Windows Hello.", deviceName),

                (>= 701 and <= 720) => string.Format(ResourceString.GetString("diag_hw_analysis_701") ??
                    "TPM Dictionary Attack Lockout Mode. '{0}' has locked itself to prevent an attacker from brute-forcing its cryptographic keys. " +
                    "All cryptographic operations on the chip are suspended.\n\n" +
                    "RECOMMENDATION: You must wait for the TPM lockout timer to expire, or clear the TPM in the BIOS.", deviceName),

                (>= 721 and <= 740) => string.Format(ResourceString.GetString("diag_hw_analysis_721") ??
                    "TPM Random Number Generator (RNG) Failure. The hardware entropy source inside '{0}' has failed, preventing secure key generation.\n\n" +
                    "RECOMMENDATION: Update the motherboard BIOS or TPM firmware.", deviceName),

                (>= 741 and <= 760) => string.Format(ResourceString.GetString("diag_hw_analysis_741") ??
                    "Secure Enclave / TrustZone Memory Violation. An unauthorized kernel thread attempted to read the isolated memory space of '{0}'. " +
                    "The CPU has triggered a strict hardware halt to protect sensitive data.\n\n" +
                    "RECOMMENDATION: This is a severe security event. Run a deep system malware scan immediately.", deviceName),

                (>= 761 and <= 780) => string.Format(ResourceString.GetString("diag_hw_analysis_761") ??
                    "Virtualization-Based Security (VBS) Credential Guard Fault. '{0}' failed to communicate with the isolated secure kernel. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the Local Security Authority (LSA) isolate.", deviceName),

                (>= 781 and <= 800) => string.Format(ResourceString.GetString("diag_hw_analysis_781") ??
                    "Cryptographic Key Attestation Timeout. '{0}' failed to provide its cryptographic endorsement key to the OS within the required timeframe. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state.", deviceName),

                (>= 801 and <= 835) => string.Format(ResourceString.GetString("diag_hw_analysis_801") ??
                    "Microsoft Pluton / Hardware Security Processor Communication Drop. The OS lost contact with the deep-level security processor in '{0}'. \n\n" +
                    "RECOMMENDATION: Reboot the system.", deviceName),

                (>= 836 and <= 860) => string.Format(ResourceString.GetString("diag_hw_analysis_836") ??
                    "NVMe Controller Fatal Status (CFS) Flag Set. The firmware for '{0}' encountered a catastrophic internal error and has completely halted. " +
                    "The drive has taken itself offline to prevent permanent data corruption.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle. If this repeats, the SSD is dying.", deviceName),

                (>= 861 and <= 900) => string.Format(ResourceString.GetString("diag_hw_analysis_861") ??
                    "NVMe PCIe Link State Power Management (ASPM) Drop. '{0}' entered a low-power state but failed to wake up, dropping off the PCIe bus completely.\n\n" +
                    "RECOMMENDATION: Disable PCIe Link State Power Management in the Windows Power Plan.", deviceName),

                (>= 901 and <= 940) => string.Format(ResourceString.GetString("diag_hw_analysis_901") ??
                    "NVMe Completion Queue (CQ) Timeout. '{0}' failed to process a read/write command in time. " +
                    "The NVMe driver is waiting for a response, leading to a controller freeze and a massive system DPC latency spike (100% Active Time in Task Manager).\n\n" +
                    "RECOMMENDATION: Update the firmware of your NVMe SSD via the manufacturer's dashboard.", deviceName),

                (>= 941 and <= 980) => string.Format(ResourceString.GetString("diag_hw_analysis_941") ??
                    "NVMe Submission Queue (SQ) Overflow. The OS submitted too many I/O commands to '{0}', overflowing the drive's hardware queue.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the storage controller.", deviceName),

                (>= 981 and <= 1020) => string.Format(ResourceString.GetString("diag_hw_analysis_981") ??
                    "SATA AHCI Native Command Queuing (NCQ) Stall. '{0}' dropped a queued command, forcing the OS to reset the entire SATA port link. " +
                    "This causes a hard stutter across the entire operating system.\n\n" +
                    "RECOMMENDATION: Try using a different SATA port on the motherboard or replacing the SATA cable.", deviceName),

                (>= 1021 and <= 1060) => string.Format(ResourceString.GetString("diag_hw_analysis_1021") ??
                    "SATA CRC Error / Cable Fault. The SATA controller detected data corruption during transit to '{0}'. " +
                    "The packet was discarded, and the OS will attempt a retry, degrading performance.\n\n" +
                    "RECOMMENDATION: Replace the SATA cable immediately.", deviceName),

                (>= 1061 and <= 1100) => string.Format(ResourceString.GetString("diag_hw_analysis_1061") ??
                    "Physical Sector Remap / SMART Trip Prediction. The internal diagnostics for '{0}' indicate that physical NAND flash or magnetic platters are heavily degraded. " +
                    "The drive is actively moving data away from dead sectors.\n\n" +
                    "RECOMMENDATION: Immediate data backup is required. The drive is operating in a degraded failure state.", deviceName),

                (>= 1101 and <= 1169) => string.Format(ResourceString.GetString("diag_hw_analysis_1101") ??
                    "Storage Device Buffer Flush Timeout. '{0}' took too long to flush its volatile DRAM cache to permanent non-volatile storage. " +
                    "If power is lost right now, data corruption is highly likely.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a hardware cache flush.", deviceName),

                // DISPLAYS, SENSORS & EMBEDDED CONTROLLERS (1170 - 1835)

                (>= 1170 and <= 1200) => string.Format(ResourceString.GetString("diag_hw_analysis_1170") ??
                    "DisplayPort DPCD Link Training Failure. The graphics card failed to negotiate a stable bandwidth lane with '{0}'. " +
                    "The signaling rate dropped. This causes flickering, black screens, or heavily reduced refresh rates (e.g., 4K stuck at 30Hz).\n\n" +
                    "RECOMMENDATION: Click 'Fix' to force the graphics driver to rescan the display output ports. Use a VESA-certified cable.", deviceName),

                (>= 1201 and <= 1250) => string.Format(ResourceString.GetString("diag_hw_analysis_1201") ??
                    "DisplayPort Multi-Stream Transport (MST) Hub Topology Error. The system failed to map the daisy-chained monitors connected to '{0}'. \n\n" +
                    "RECOMMENDATION: Disconnect and reconnect the primary monitor in the MST chain.", deviceName),

                (>= 1251 and <= 1300) => string.Format(ResourceString.GetString("diag_hw_analysis_1251") ??
                    "HDMI Hot-Plug Detect (HPD) Bounce. '{0}' is rapidly asserting and de-asserting its presence on the graphics port. " +
                    "The OS is constantly attempting to reconfigure the desktop layout, causing severe stuttering.\n\n" +
                    "RECOMMENDATION: The HDMI cable or port is physically loose or damaged.", deviceName),

                (>= 1301 and <= 1350) => string.Format(ResourceString.GetString("diag_hw_analysis_1301") ??
                    "HDMI TMDS Clock Drop. The clock signal driving pixels to '{0}' lost synchronization, resulting in 'snow' or artifacts on the screen.\n\n" +
                    "RECOMMENDATION: Lower the display refresh rate or color depth.", deviceName),

                (>= 1351 and <= 1400) => string.Format(ResourceString.GetString("diag_hw_analysis_1351") ??
                    "EDID / DisplayID Corrupted Checksum. The system failed to read the specific monitor properties (resolution, refresh rate, HDR capabilities) from '{0}'. " +
                    "The connection is unstable, and Windows will default to a basic 1080p/60Hz output.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to cleanly rebuild the registry parameters and clear the monitor cache.", deviceName),

                (>= 1401 and <= 1450) => string.Format(ResourceString.GetString("diag_hw_analysis_1401") ??
                    "HDR / Advanced Color Metadata Handshake Failure. '{0}' claims to support HDR, but the graphics driver cannot validate its luminance metadata.\n\n" +
                    "RECOMMENDATION: Toggle HDR off and on in Windows Display Settings.", deviceName),

                (>= 1451 and <= 1480) => string.Format(ResourceString.GetString("diag_hw_analysis_1451") ??
                    "GPU Framebuffer Memory Allocation Fault. The system could not assign a contiguous memory block in VRAM for '{0}'. " +
                    "The monitor cannot be driven because there is no memory space available to draw the desktop.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle, invalidate the current driver state, and securely reboot the endpoint.", deviceName),

                (>= 1481 and <= 1502) => string.Format(ResourceString.GetString("diag_hw_analysis_1481") ??
                    "GPU Display Core Hang. The silicon responsible for pumping pixels out to '{0}' has frozen independently of the main 3D rendering core.\n\n" +
                    "RECOMMENDATION: Restart the graphics driver (Win + Ctrl + Shift + B).", deviceName),

                (>= 1503 and <= 1550) => string.Format(ResourceString.GetString("diag_hw_analysis_1503") ??
                    "ACPI Thermal Zone Critical Trip Point Reached. '{0}' reported temperatures exceeding safety limits. " +
                    "The OS may aggressively throttle the CPU or force an immediate thermal shutdown to prevent fire or melting.\n\n" +
                    "RECOMMENDATION: Clean dust from heatsinks, repaste the CPU/GPU, and ensure fans are spinning.", deviceName),

                (>= 1551 and <= 1600) => string.Format(ResourceString.GetString("diag_hw_analysis_1551") ??
                    "ACPI Thermal Passive Trip Point. '{0}' is getting hot. The OS has initiated mild CPU throttling and spun up cooling fans to compensate.\n\n" +
                    "RECOMMENDATION: Ensure adequate airflow around the system.", deviceName),

                (>= 1601 and <= 1650) => string.Format(ResourceString.GetString("diag_hw_analysis_1601") ??
                    "Embedded Controller (EC) RAM Read Timeout. The motherboard sensor subsystem for '{0}' stopped reporting data to the OS. " +
                    "Fan speeds and battery percentages may stop updating completely.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully uninstall the device node and rebuild its registry parameters.", deviceName),

                (>= 1651 and <= 1700) => string.Format(ResourceString.GetString("diag_hw_analysis_1651") ??
                    "Embedded Controller (EC) Battery Communication Drop. '{0}' (the battery) is no longer communicating its charge status or voltage via the SMBus.\n\n" +
                    "RECOMMENDATION: Perform a hard reset by holding the laptop power button for 30 seconds.", deviceName),

                (>= 1701 and <= 1750) => string.Format(ResourceString.GetString("diag_hw_analysis_1701") ??
                    "Ambient Light Sensor (ALS) Calibration Lost. '{0}' is outputting raw, uncalibrated lux data, breaking the Windows auto-brightness feature.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to trigger a Plug-and-Play (PnP) rescan and reload sensor profiles.", deviceName),

                (>= 1751 and <= 1800) => string.Format(ResourceString.GetString("diag_hw_analysis_1751") ??
                    "Accelerometer / Gyroscope Sensor Fusion Failure. The orientation data from '{0}' is desynchronized. " +
                    "The screen will fail to auto-rotate when a tablet/2-in-1 device is turned.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the Windows Sensor Data Service.", deviceName),

                (>= 1801 and <= 1835) => string.Format(ResourceString.GetString("diag_hw_analysis_1801") ??
                    "Motherboard VRM I2C Telemetry Loss. Communication with the voltage regulation modules (VRMs) for '{0}' failed. " +
                    "Software can no longer monitor CPU voltage or VRM temperatures.\n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the device connection state.", deviceName),

                // THE LEFTOVER INDIVIDUAL CODES

                2 or 6 or 8 or 9 or 11 or 18 or 20 or 25 or 26 or 27 or 30 or 34 or 35 or 36 or 40 or 42 or 44 or 46 or 50 or 51 or 54 or 55 or 56 or 57 or 58 => string.Format(ResourceString.GetString("diag_hw_analysis_generic_conflict") ??
                    "Hardware State Conflict. '{0}' has reported a generalized initialization failure, a power state conflict, or a driver crash. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep device power cycle, invalidate the current driver state, and securely reboot the endpoint.", deviceName),

                13 or 15 or 16 or 17 or 23 or 29 or 33 or 47 or 53 or 59 or 60 or 61 or 62 or 63 or 69 or 70 or 71 or 72 or 73 => string.Format(ResourceString.GetString("diag_hw_analysis_generic_bus") ??
                    "Bus Enumeration Error. The active hardware tree in Windows is currently out of sync with the physical connections for '{0}'. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to force a strict hardware tree rescan and force the PnP manager to re-enumerate the device bus.", deviceName),

                3 or 48 or 64 or 65 or 66 or 67 or 68 => string.Format(ResourceString.GetString("diag_hw_analysis_generic_config") ??
                    "Device Configuration Fault. The local machine hive governing '{0}' contains structurally locked or corrupted data parameters. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully uninstall the ghosted device node, strip its configuration, and cleanly rebuild its registry parameters from scratch.", deviceName),

                // Absolute Fallback
                _ => string.Format(ResourceString.GetString("diag_hw_analysis_fallback") ??
                    "A hardware fault code ({0}) was detected for '{1}'. The Remediation Engine is standing by to cycle the device.", wmiErrorCode, deviceName)
            };
        }

        private static string GenerateServiceAnomalyAnalysis(int eventId, string serviceName)
        {
            string targetName = new SystemAnomalySolver().GetServiceFriendlyName(serviceName);

            return eventId switch
            {
                // NATIVE WINDOWS SERVICE CONTROL MANAGER (SCM) FAULTS (7000 - 7099)

                7000 => string.Format(ResourceString.GetString("diag_scm_analysis_7000") ??
                    "Service Control Manager (SCM) Boot Failure. The '{0}' service failed to start due to an internal execution error. " +
                    "The binary executable attempted to map into memory but encountered a fatal fault before registering its Process ID with the OS kernel. " +
                    "This is often caused by missing DLLs or severe file corruption. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcibly rebuild the service registry key and verify the executable signature.", targetName),

                7001 => string.Format(ResourceString.GetString("diag_scm_analysis_7001") ??
                    "Dependency Chain Cascade Failure. The '{0}' service relies on a parent service that has failed to start. " +
                    "Windows architecture is strictly hierarchical; if the foundation (like the RPC Endpoint Mapper or Network Store) is down, all child services will instantly cascade fail. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to trace the dependency tree and restart the broken parent service.", targetName),

                7002 => string.Format(ResourceString.GetString("diag_scm_analysis_7002") ??
                    "Load Order Group Dependency Failure. '{0}' requires an entire group of system drivers to initialize before it can start. " +
                    "One or more drivers in this strict load order failed during the kernel boot phase, stalling this service. \n\n" +
                    "RECOMMENDATION: Review the System Event Log for failing boot-start drivers (Event 7026).", targetName),

                7003 => string.Format(ResourceString.GetString("diag_scm_analysis_7003") ??
                    "Missing Dependency. The '{0}' service depends on another service that physically does not exist in the registry. " +
                    "This usually happens when aggressive debloat scripts or improper uninstallers delete core Windows components. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to remove the ghost dependency from the service's registry parameters.", targetName),

                7005 => string.Format(ResourceString.GetString("diag_scm_analysis_7005") ??
                    "SCM LoadOrderGroup Error. The Service Control Manager encountered an invalid group name while trying to establish the boot sequence for '{0}'. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restore default Windows LoadOrderGroup definitions.", targetName),

                7006 => string.Format(ResourceString.GetString("diag_scm_analysis_7006") ??
                    "Registry Write Failure. The Service Control Manager was denied access to update the startup status of '{0}' in the registry. " +
                    "This indicates a severe permission lock, likely caused by aggressive antivirus software protecting the HKLM\\SYSTEM hive. \n\n" +
                    "RECOMMENDATION: Check antivirus logs for blocked registry modifications.", targetName),

                7008 => string.Format(ResourceString.GetString("diag_scm_analysis_7008") ??
                    "Circular Dependency Detected. '{0}' is trapped in an infinite loop. Service A depends on Service B, and Service B depends on Service A. " +
                    "The SCM has aborted the startup sequence to prevent a complete system lockup. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to sever the circular dependency chain in the registry.", targetName),

                7009 => string.Format(ResourceString.GetString("diag_scm_analysis_7009") ??
                    "Service Start Timeout. '{0}' failed to respond to the Service Control Manager within the default 30,000 millisecond (30 second) window. " +
                    "This occurs on heavily bottlenecked CPUs, failing hard drives, or when a service is trapped in an endless initialization loop. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to extend the 'ServicesPipeTimeout' registry key to 60 seconds.", targetName),

                7011 => string.Format(ResourceString.GetString("diag_scm_analysis_7011") ??
                    "Service Transaction Timeout. The '{0}' service is running, but it took too long to process an incoming transaction request from the OS. " +
                    "The service's background thread is likely deadlocked or starved for RAM. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to gracefully restart the service and clear deadlocked threads.", targetName),

                7016 => string.Format(ResourceString.GetString("diag_scm_analysis_7016") ??
                    "Invalid State Transition. '{0}' reported an impossible state to the kernel (e.g., transitioning directly from 'Stopped' to 'Paused'). \n\n" +
                    "RECOMMENDATION: The service executable is malfunctioning. Check for software updates.", targetName),

                7022 => string.Format(ResourceString.GetString("diag_scm_analysis_7022") ??
                    "Service Hung During Initialization. '{0}' started its boot sequence but became completely unresponsive before reporting a 'Running' state. " +
                    "The OS has suspended the thread to prevent it from consuming 100% CPU. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to kill the suspended process and attempt a cold restart of the service.", targetName),

                7023 => string.Format(ResourceString.GetString("diag_scm_analysis_7023") ??
                    "Fatal Service Termination. '{0}' terminated unexpectedly and returned a specific Win32 exit code. " +
                    "This is a crash. The service encountered a condition it could not handle and voluntarily shut itself down. \n\n" +
                    "RECOMMENDATION: Check the Windows Application Event Log for a corresponding Application Crash (Event 1000).", targetName),

                7024 => string.Format(ResourceString.GetString("diag_scm_analysis_7024") ??
                    "Service-Specific Exit Code. '{0}' terminated and returned a custom error code defined by its developer, not the standard Windows kernel. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to analyze the developer-specific crash parameters.", targetName),

                7026 => string.Format(ResourceString.GetString("diag_scm_analysis_7026") ??
                    "Kernel Driver Load Failure. A boot-start or system-start driver associated with '{0}' failed to map into memory during the early OS boot phase. " +
                    "This is critical. A hardware component or low-level security filter is completely offline. \n\n" +
                    "RECOMMENDATION: Review recent driver updates or system hardware changes.", targetName),

                7030 => string.Format(ResourceString.GetString("diag_scm_analysis_7030") ??
                    "Session 0 Isolation Violation. '{0}' is configured as an 'Interactive Service', attempting to draw a UI directly to the desktop. " +
                    "Modern Windows security (Session 0 Isolation) strictly blocks background services from interacting with the user desktop to prevent shatter attacks. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to revoke the 'Allow service to interact with desktop' permission.", targetName),

                7031 => string.Format(ResourceString.GetString("diag_scm_analysis_7031") ??
                    "Unexpected Service Crash (First Occurrence). '{0}' crashed abruptly. The Service Control Manager has detected the failure and will attempt to apply the first defined recovery action (usually an auto-restart). \n\n" +
                    "RECOMMENDATION: Monitor system stability. If this repeats, the binary is corrupted.", targetName),

                7032 => string.Format(ResourceString.GetString("diag_scm_analysis_7032") ??
                    "Unexpected Service Crash (Repeated Occurrence). '{0}' has crashed for a second time. The OS is applying the secondary recovery action. " +
                    "The service is highly unstable and is entering a crash-loop. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to isolate the service and prevent it from crashing the broader svchost.exe pool.", targetName),

                7034 => string.Format(ResourceString.GetString("diag_scm_analysis_7034") ??
                    "Abnormal Service Termination. '{0}' terminated abruptly without passing through the standard SCM stop controls. " +
                    "The process was either killed by an Administrator in Task Manager, or terminated by an aggressive antivirus. \n\n" +
                    "RECOMMENDATION: Verify if this process termination was intentional.", targetName),

                7036 => string.Format(ResourceString.GetString("diag_scm_analysis_7036") ??
                    "Service State Transition. '{0}' successfully entered the stopped or running state. " +
                    "This is standard operational telemetry logging the normal lifecycle of background daemons. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry. No action required.", targetName),

                7040 => string.Format(ResourceString.GetString("diag_scm_analysis_7040") ??
                    "Startup Type Modification. The boot configuration for '{0}' was changed (e.g., from Automatic to Disabled). " +
                    "This was explicitly triggered by a user, an installer, or a system optimization tool. \n\n" +
                    "RECOMMENDATION: Verify that this modification was intended.", targetName),

                7041 => string.Format(ResourceString.GetString("diag_scm_analysis_7041") ??
                    "Service Account Password Rejection. '{0}' failed to start because the associated user account password in the registry is incorrect or has expired. \n\n" +
                    "RECOMMENDATION: Open Services.msc, locate the service, and update the credentials in the 'Log On' tab.", targetName),

                7042 => string.Format(ResourceString.GetString("diag_scm_analysis_7042") ??
                    "Service Paused. '{0}' was successfully transitioned into a paused state. " +
                    "It remains loaded in RAM but has suspended its background worker threads. \n\n" +
                    "RECOMMENDATION: No action required.", targetName),

                7043 => string.Format(ResourceString.GetString("diag_scm_analysis_7043") ??
                    "Service Control Rejection. The Service Control Manager sent a command (like Stop or Pause) to '{0}', but the service explicitly rejected it. \n\n" +
                    "RECOMMENDATION: The service may be heavily locked or currently processing uninterruptible I/O.", targetName),

                7045 => string.Format(ResourceString.GetString("diag_scm_analysis_7045") ??
                    "New Service Installation Detected. A new background service or kernel driver associated with '{0}' was written to the system registry. " +
                    "Attackers often install malicious services to establish persistence. \n\n" +
                    "RECOMMENDATION: If you did not just install new software, run a malware scan immediately.", targetName),

                7050 => string.Format(ResourceString.GetString("diag_scm_analysis_7050") ??
                    "Logon Failure. '{0}' cannot initialize because the Local Security Authority (LSA) rejected its logon attempt. \n\n" +
                    "RECOMMENDATION: Verify the integrity of the local service accounts.", targetName),

                7051 => string.Format(ResourceString.GetString("diag_scm_analysis_7051") ??
                    "Service Account Disabled. '{0}' attempted to start, but the specific user account it is configured to use has been disabled by an Administrator. \n\n" +
                    "RECOMMENDATION: Change the service logon to 'Local System' or 'Network Service'.", targetName),

                7052 => string.Format(ResourceString.GetString("diag_scm_analysis_7052") ??
                    "Missing 'Logon as a Service' Right. '{0}' cannot start because its account lacks the SeServiceLogonRight privilege in the local group policy. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to re-grant the 'Log on as a service' right to the account.", targetName),

                (>= 7053 and <= 7099) => string.Format(ResourceString.GetString("diag_scm_analysis_7053") ??
                    "Generic Service Control Manager (SCM) telemetry or minor transition state logging for '{0}'. \n\n" +
                    "RECOMMENDATION: Routine OS overhead telemetry.", targetName),

                // THE EVOLVE-OS "GHOSTING" ENGINE (7100 - 7199)

                7100 => string.Format(ResourceString.GetString("diag_scm_analysis_7100") ??
                    "A 'Ghosting' state mismatch was detected for '{0}'. " +
                    "The registry dictates this service should be running automatically, but the live Service Control Manager reports it has crashed or stopped. " +
                    "This indicates sudden resource starvation or a failing background module. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcibly awaken the service and clear the deadlock.", targetName),

                7101 => string.Format(ResourceString.GetString("diag_scm_analysis_7101") ??
                    "Process ID (PID) Detachment. The executable for '{0}' is running in RAM, but it has completely detached from the Service Control Manager. " +
                    "Windows has lost the ability to monitor, stop, or restart the service. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully terminate the orphaned PID and attach a new service host.", targetName),

                7102 => string.Format(ResourceString.GetString("diag_scm_analysis_7102") ??
                    "Orphaned Handle Detection. '{0}' has crashed, but it left behind open handles to files or registry keys. " +
                    "These locked handles are preventing the service from restarting successfully. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a deep handle release protocol and free the locked resources.", targetName),

                7103 => string.Format(ResourceString.GetString("diag_scm_analysis_7103") ??
                    "Zombie Process Thread Lock. The primary thread for '{0}' is dead, but a child worker thread refuses to exit. " +
                    "The service is trapped in a 'Zombie' state, consuming RAM but doing no actual work. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a thread-level termination command.", targetName),

                7104 => string.Format(ResourceString.GetString("diag_scm_analysis_7104") ??
                    "Service Control Manager Desynchronization. The internal SCM database thinks '{0}' is running, but the kernel reports the process does not exist. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to re-sync the SCM database with the active kernel process list.", targetName),

                7105 => string.Format(ResourceString.GetString("diag_scm_analysis_7105") ??
                    "Memory Mapped Ghost. '{0}' has been uninstalled, but its core DLLs are still mapped into the active memory of another running application. \n\n" +
                    "RECOMMENDATION: Restart the PC to clear the memory map, or click 'Fix' to flush the DLL cache.", targetName),

                7106 => string.Format(ResourceString.GetString("diag_scm_analysis_7106") ??
                    "RPC Endpoint Mapping Ghost. '{0}' is actively listening on a Remote Procedure Call port, but the service itself is supposed to be disabled. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to terminate the rogue RPC listener and secure the port.", targetName),

                7107 => string.Format(ResourceString.GetString("diag_scm_analysis_7107") ??
                    "Named Pipe Connection Drop. '{0}' lost connection to its local IPC (Inter-Process Communication) named pipe. It is running completely blind. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the service and re-establish the IPC pipeline.", targetName),

                7108 => string.Format(ResourceString.GetString("diag_scm_analysis_7108") ??
                    "Thread Pool Exhaustion State. '{0}' has generated so many asynchronous background threads that it exhausted its allocated thread pool. " +
                    "The service is ghosting because it cannot process new requests. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to throttle the service priority and reset the thread pool.", targetName),

                7109 => string.Format(ResourceString.GetString("diag_scm_analysis_7109") ??
                    "GDI / USER Handle Ghosting. '{0}' has leaked thousands of UI handles, hitting the hardcoded OS limit of 10,000. " +
                    "The service can no longer draw windows or render memory. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to gracefully kill the service and clear the UI handle leak.", targetName),

                (>= 7110 and <= 7199) => string.Format(ResourceString.GetString("diag_scm_analysis_7110") ??
                    "Minor Ghosting or asynchronous desynchronization detected for '{0}'. The service is experiencing minor latency but remains operational. \n\n" +
                    "RECOMMENDATION: The Remediation Engine will continue monitoring the service state.", targetName),

                // INTEGRITY COMPROMISE & SECURITY ENGINE (7200 - 7299)

                7200 => string.Format(ResourceString.GetString("diag_scm_analysis_7200") ??
                    "⚠️ INTEGRITY COMPROMISE DETECTED. The execution path (ImagePath) for '{0}' does not point to a secure Windows directory (System32/SysWOW64). " +
                    "This usually means aggressive debloat software or malware has hijacked the service route. \n\n" +
                    "RECOMMENDATION: Immediate remediation required to prevent rogue code execution.", targetName),

                7201 => string.Format(ResourceString.GetString("diag_scm_analysis_7201") ??
                    "⚠️ UNSIGNED DLL INJECTION. A third-party dynamic library without a valid digital signature has injected itself into the memory space of '{0}'. " +
                    "This is a common tactic for rootkits or aggressive game anti-cheat engines. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to isolate the process and run an integrity validation check.", targetName),

                7202 => string.Format(ResourceString.GetString("diag_scm_analysis_7202") ??
                    "⚠️ SECURITY DESCRIPTOR OVERWRITE. The Access Control List (ACL) for '{0}' was maliciously altered. " +
                    "Standard users now have the power to stop, pause, or delete this critical system service. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restore the default strict Security Descriptor Definition Language (SDDL) strings.", targetName),

                7203 => string.Format(ResourceString.GetString("diag_scm_analysis_7203") ??
                    "⚠️ SVCHOST HIJACK. '{0}' is configured to run as a shared process inside svchost.exe, but its ServiceDll parameter points to an unauthorized payload. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to quarantine the rogue DLL and restore the native binary path.", targetName),

                7204 => string.Format(ResourceString.GetString("diag_scm_analysis_7204") ??
                    "⚠️ EXECUTION PREVENTION (DEP) VIOLATION. '{0}' attempted to execute code from a memory region marked as 'Data Only'. " +
                    "This strongly indicates a buffer overflow exploit attempt against the service. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully terminate the compromised service and enforce strict DEP policies.", targetName),

                7205 => string.Format(ResourceString.GetString("diag_scm_analysis_7205") ??
                    "⚠️ PRIVILEGE ESCALATION HIJACK. '{0}' normally runs as 'Network Service', but its registry keys were altered to run as 'Local System' (NT AUTHORITY\\SYSTEM). \n\n" +
                    "RECOMMENDATION: Click 'Fix' to immediately demote the service back to its secure, low-privilege account.", targetName),

                7206 => string.Format(ResourceString.GetString("diag_scm_analysis_7206") ??
                    "⚠️ BINARY CHECKSUM MISMATCH. The physical executable on the disk for '{0}' does not match the cryptographic hash stored in the Windows Catalog. " +
                    "The file has been tampered with. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to trigger an online DISM repair and replace the corrupted binary.", targetName),

                7207 => string.Format(ResourceString.GetString("diag_scm_analysis_7207") ??
                    "⚠️ DIGITAL SIGNATURE REVOCATION. The security certificate signing '{0}' has been explicitly revoked by Microsoft or the publisher. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to quarantine the service. Do not run revoked software.", targetName),

                7208 => string.Format(ResourceString.GetString("diag_scm_analysis_7208") ??
                    "⚠️ ROOTKIT CLOAKING DETECTED. '{0}' is attempting to hide its active process from Task Manager and the Windows API using low-level kernel hooks. \n\n" +
                    "RECOMMENDATION: This is a severe security breach. Run an offline Windows Defender scan immediately.", targetName),

                7209 => string.Format(ResourceString.GetString("diag_scm_analysis_7209") ??
                    "⚠️ FILELESS REGISTRY EXECUTION. '{0}' contains no physical executable. It is entirely composed of malicious PowerShell or JavaScript embedded directly in the registry (RunPE). \n\n" +
                    "RECOMMENDATION: Click 'Fix' to purge the malicious registry keys and secure the OS.", targetName),

                (>= 7210 and <= 7299) => string.Format(ResourceString.GetString("diag_scm_analysis_7210") ??
                    "Minor integrity warning for '{0}'. The service binary is healthy, but an associated configuration key has anomalous permissions. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to normalize service permissions.", targetName),

                // FAIL-SAFE & RECOVERY PROTOCOL DEGRADATION (7300 - 7399)

                7300 => string.Format(ResourceString.GetString("diag_scm_analysis_7300") ??
                    "The fail-safe recovery protocols for '{0}' have been wiped. " +
                    "Normally, if a service crashes, Windows automatically restarts it. Because these protocols are missing, a single micro-crash will permanently kill this service until a system reboot. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the native crash-recovery registry keys.", targetName),

                7301 => string.Format(ResourceString.GetString("diag_scm_analysis_7301") ??
                    "Auto-Restart Action Disabled. '{0}' crashed, but the SCM is explicitly configured to 'Take No Action' upon failure. " +
                    "Crucial background capabilities are remaining offline. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to configure the service to 'Restart the Service' on First and Second failures.", targetName),

                7302 => string.Format(ResourceString.GetString("diag_scm_analysis_7302") ??
                    "Crash Count Reset Failure. The failure counter for '{0}' has not reset. " +
                    "The service has crashed so many times that Windows has exhausted its recovery attempts and given up. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to reset the fail-count to zero and analyze why the service is crash-looping.", targetName),

                7303 => string.Format(ResourceString.GetString("diag_scm_analysis_7303") ??
                    "Recovery Command-Line Execution Deleted. '{0}' is supposed to run a specific script or repair tool when it fails, but the command path is missing or invalid. \n\n" +
                    "RECOMMENDATION: Restore the correct recovery script path in the service properties.", targetName),

                7304 => string.Format(ResourceString.GetString("diag_scm_analysis_7304") ??
                    "Reboot-On-Crash Protocol Bypassed. '{0}' is a critical subsystem (like LSASS or CSRSS) that must trigger a BugCheck (BSOD) if it fails. This protocol has been tampered with. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restore the critical system boot-flag.", targetName),

                7305 => string.Format(ResourceString.GetString("diag_scm_analysis_7305") ??
                    "Failure Actions Registry Key Locked. The OS attempted to update the crash-handling logic for '{0}', but the registry key is locked or read-only. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to force permission inheritance on the FailureActions registry node.", targetName),

                7306 => string.Format(ResourceString.GetString("diag_scm_analysis_7306") ??
                    "Dependent Service Recovery Halt. '{0}' recovered successfully, but it failed to automatically wake up the child services that depend on it. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to execute a cascading wake command across the entire dependency tree.", targetName),

                (>= 7307 and <= 7399) => string.Format(ResourceString.GetString("diag_scm_analysis_7307") ??
                    "Minor recovery protocol desynchronization for '{0}'. The service will attempt standard restarts, but advanced logging is disabled. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restore default OS recovery behaviors.", targetName),

                // BROKEN DEPENDENCY HIERARCHY (7400 - 7499)

                7400 => string.Format(ResourceString.GetString("diag_scm_analysis_7400") ??
                    "'{0}' is healthy, but its required dependency chain is broken. " +
                    "A parent service it relies on has been disabled. Windows architecture is strictly hierarchical; if the foundation is disabled, the child services will cascade fail. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to trace and repair the broken dependency hierarchy.", targetName),

                7401 => string.Format(ResourceString.GetString("diag_scm_analysis_7401") ??
                    "Parent Service Explicitly Disabled. '{0}' cannot start because its primary dependency was manually set to 'Disabled' in the registry. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to revert the parent service startup type to 'Manual' or 'Automatic'.", targetName),

                7402 => string.Format(ResourceString.GetString("diag_scm_analysis_7402") ??
                    "Grandparent Service Uninstalled. The deep dependency chain for '{0}' is fundamentally broken because a core Windows component (like the RPC mapper) has been removed. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to run a deep DISM restoration and replace missing core drivers.", targetName),

                7403 => string.Format(ResourceString.GetString("diag_scm_analysis_7403") ??
                    "Tag-Ordered Loading Disrupted. '{0}' attempted to load out of order during boot. It tried to start before its required kernel drivers were initialized in memory. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to rebuild the GroupOrderList in the registry.", targetName),

                7404 => string.Format(ResourceString.GetString("diag_scm_analysis_7404") ??
                    "Network Location Awareness Dependency Drop. '{0}' requires active network connectivity to start, but the NLA (Network Location Awareness) service is dead or hung. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to restart the NLA service and refresh the network stack.", targetName),

                7405 => string.Format(ResourceString.GetString("diag_scm_analysis_7405") ??
                    "Cryptographic Dependency Drop. '{0}' requires secure decryption capabilities to boot, but the Cryptographic Services (CryptSvc) foundation is failing. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to repair the Catroot2 folders and restart CryptSvc.", targetName),

                7406 => string.Format(ResourceString.GetString("diag_scm_analysis_7406") ??
                    "DCOM Server Dependency Failure. '{0}' relies on a COM object that failed to register its class ID during OS initialization. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to forcefully re-register the target CLSIDs.", targetName),

                7407 => string.Format(ResourceString.GetString("diag_scm_analysis_7407") ??
                    "Third-Party Filter Dependency Missing. '{0}' requires a specific antivirus or VPN filter driver to load, but the driver was improperly uninstalled. \n\n" +
                    "RECOMMENDATION: Click 'Fix' to remove the orphaned filter dependency from the registry.", targetName),

                (>= 7408 and <= 7499) => string.Format(ResourceString.GetString("diag_scm_analysis_7408") ??
                    "Generic dependency warning for '{0}'. The service experienced a minor delay waiting for a parent subsystem to initialize. \n\n" +
                    "RECOMMENDATION: Routine OS boot telemetry. No action required.", targetName),

                // Absolute Fallback
                _ => string.Format(ResourceString.GetString("diag_scm_analysis_fallback") ??
                    "An unknown telemetry anomaly (Code {0}) was detected regarding '{1}'. " +
                    "The Remediation Engine is standing by to stabilize the service state and ensure background capabilities remain online. \n\n" +
                    "RECOMMENDATION: Monitor system stability.", eventId, targetName)
            };
        }
    }
}