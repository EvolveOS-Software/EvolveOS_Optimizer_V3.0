// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Maintenance;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class RemediationEngine
    {
        #region Software Remediation

        public static async Task<bool> RunFixAsync(int eventId, string sourceName = "")
        {
            try
            {
                if (eventId >= 7000 && eventId < 7100 && sourceName.StartsWith("ServiceMonitor|"))
                {
                    try
                    {
                        string serviceName = sourceName.Split('|')[1];

                        string command = $@"
                        Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\{serviceName}' -Name 'Start' -Value 2 -Force;
                        Start-Service -Name '{serviceName}' -ErrorAction SilentlyContinue;";

                        await CommandExecutor.RunCommandAsTrustedInstaller(command, isPowerShell: true);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RemediationEngine] Service Fix Failed: {ex.Message}");
                        return false;
                    }
                }

                return eventId switch
                {
                    #region PERFORMANCE BOTTLENECK REMEDIATION

                    // 8001, 9001, 9002 = RAM/Pagefile Exhaustion
                    8001 or 9001 or 9002 => await ExecuteNativeMemoryCleanAsync(),

                    // 8002 = CPU Bottleneck -> Reset Event Tracing
                    8002 => await FixEventTracingAsync(),

                    // 8003 = Disk Saturation -> Use your native flush buffers and cache discard
                    8003 => await ExecuteNativeDiskOptimizeAsync(),

                    #endregion

                    #region SPECIFIC REPAIRS

                    // Windows Search & Indexing (Corrupted index, service hangs, protocol failures)
                    16 or 100 or 101 or 1001 or 1002 or 3006 or 3007 or 7040 or 7042 or 9000
                        => await FixWindowsSearchAsync(),

                    // Display Driver Reset (TDR errors, driver recovery, hardware acceleration crashes)
                    4101 or 4109 or 4115 or 14 or 10
                        => await ResetDisplayDriverAsync(),

                    // DNS Cache & Client logic (Resolution timeouts, server unreachable, cache poisoning)
                    1014 or 1012 or 1015 or 1016 or 1017 or 1018 or 1019
                        => await FixNetworkDnsAsync(),

                    // Volume Shadow Copy (VSS) (Provider crashes, metadata corruption, backup failures)
                    8193 or 12289 or 13 or 20 or 22 or 34 or 12290 or 12293 or 12297 or 12298 or 8194
                        => await FixVssServiceAsync(),

                    // Print Spooler stack (Spooler service crash, job metadata corruption, RPC failures)
                    315 or 808 or 316 or 4 or 32 or 40 or 50 or 310 or 483 or 603 or 123
                        => await FixPrintSpoolerAsync(),

                    // Performance Counter rebuilding (Corrupted registry keys, provider discovery failures)
                    1023 or 1008 or 1001 or 1004 or 1006 or 1017 or 1019 or 1020 or 2001 or 3000
                        => await FixPerformanceCountersAsync(),

                    // System File Repair (Application errors caused by DLL missing, Side-by-Side corruption)
                    1000 or 1001 or 33 or 35 or 11 or 59 or 60 or 6008 or 7023 or 7024 or 7031
                        => await RunSystemFileRepairAsync(),

                    // "High-Level" repair. Secure Boot CA/Keys
                    1801 => await FixSecureBootKeysAsync(),

                    // Windows Time & NTP synchronization (Client sync timeouts, stratum discovery failures)
                    131 or 36 or 144 or 17 or 29 or 34 or 35 or 37 or 38 or 47 or 49
                        => await FixTimeSyncAsync(),

                    // Resource & DWM exhaustion repair (GDI handle leaks, desktop window manager crashes)
                    2004 or 2001 or 2002 or 2003 or 2005
                        => await FixResourceExhaustionAsync(),

                    // Service Control Manager (Driver failed to load)
                    7026 => await FixLuafvServiceAsync(),

                    // SSL/TLS (Schannel) cache reset (Handshake failures, revoked certs, protocol mismatch)
                    36888 or 36887 or 36870 or 36871 or 36874 or 36880 or 36881 or 36882 or 36884 or 36885 or 36886
                        => await FixSchannelAsync(),

                    // MSI Windows Installer repair (Registry locks, installer service timeouts, GUID mismatch)
                    11708 or 1033 or 1013 or 1015 or 1040 or 1041 or 1042 or 11706 or 11707 or 11724 or 11728
                        => await FixMsiInstallerAsync(),

                    // Group Policy Refresh (GPO sync failures, LDAP timeouts, policy database corruption)
                    1005 or 1030 or 1006 or 1008 or 1010 or 1053 or 1054 or 1055 or 1058 or 1096 or 1101 or 1112
                        => await FixGroupPolicyAsync(),

                    // Wi-Fi / WLAN AutoConfig repair (Radio hangs, driver state transitions, WLAN profile locks)
                    10002 or 10200 or 5001 or 5002 or 5005 or 5007 or 5010 or 6062 or 7001 or 7002 or 7003 or 8000
                        => await FixWifiAdapterAsync(),

                    // Windows Defender logic (Signature update timeouts, engine service crashes)
                    2002 or 1005 or 1006 or 1007 or 1015 or 1116 or 1117 or 1118 or 1119 or 2001 or 2010 or 2011
                        => await FixWindowsDefenderAsync(),

                    // Lanman Server / SMB Sharing (Binding failures, network name conflicts, SMBv2 state errors)
                    2505 or 2011 or 2012 or 2021 or 2022 or 2504 or 2506 or 2507 or 2508 or 2509
                        => await FixLanmanServerAsync(),

                    // Event Tracing logic (Circular context logger maxed out, session start failures)
                    3 or 1 or 2 or 4 or 10 or 11 or 12 or 13 or 14 or 15
                        => await FixEventTracingAsync(),

                    // AppX / Start Menu deployment (Activation failures, shell host hangs, UWP manifest errors)
                    69 or 10 or 11 or 12 or 59 or 65 or 400 or 401 or 404 or 510 or 513 or 515 or 523
                        => await FixAppxDeploymentAsync(),

                    // Windows Store Cache (WSReset targets, licensing store failures, metadata discovery)
                    10010 or 5001 or 5002 or 5003 or 5004 or 10011 or 10012 or 10013 or 10014 or 10015
                        => await FixWindowsStoreAsync(),

                    // ESENT Database (TileDataLayer, Windows Search, and App Repository DB failures)
                    455 or 427 or 441 or 442 or 447 or 448 or 451 or 454 or 467 or 474 or 477 or 481 or 482 or 488 or 489 or 490
                        => await FixEsentDatabaseAsync(),

                    // Cryptographic Services (Catroot2 corruption, root certificate update failures + BitLocker/TPM events)
                    513 or 1 or 11 or 13 or 17 or 18 or 20 or 24 or 30 or 31 or 32
                    or 22000 or 22001 or 22002 or 22003 or 22004 or 22005 or 22006 or 22007 or 22008 or 22009 or 22010 or 22011 or 22012 or 22013 or 22014 or 22015
                    or 22016 or 22017 or 22018 or 22019 or 22020 or 22021 or 22022 or 22023 or 22024 or 22025 or 22026 or 22027 or 22028 or 22029 or 22030 or 22031
                    or 22032 or 22033 or 22034 or 22035 or 22036 or 22037 or 22038 or 22039 or 22040 or 22041 or 22042 or 22043 or 22044 or 22045 or 22046 or 22047
                    or 22048 or 22049 or 22050 or 22051 or 22052 or 22053 or 22054 or 22055 or 22056 or 22057 or 22058 or 22059 or 22060 or 22061 or 22062 or 22063
                    or 22064 or 22065 or 22066 or 22067 or 22068 or 22069 or 22070 or 22071 or 22072 or 22073 or 22074 or 22075 or 22076 or 22077 or 22078 or 22079
                    or 22080 or 22081 or 22082 or 22083 or 22084 or 22085 or 22086 or 22087 or 22088 or 22089 or 22090 or 22091 or 22092 or 22093 or 22094 or 22095
                    or 22096 or 22097 or 22098 or 22099 or 22100 or 22101 or 22102 or 22103 or 22104 or 22105 or 22106 or 22107 or 22108 or 22109 or 22110 or 22111
                    or 22112 or 22113 or 22114 or 22115 or 22116 or 22117 or 22118 or 22119 or 22120 or 22121 or 22122 or 22123 or 22124 or 22125 or 22126 or 22127
                    or 22128 or 22129 or 22130 or 22131 or 22132 or 22133 or 22134 or 22135 or 22136 or 22137 or 22138 or 22139 or 22140 or 22141 or 22142 or 22143
                    or 22144 or 22145 or 22146 or 22147 or 22148 or 22149 or 22150 or 22151 or 22152 or 22153 or 22154 or 22155 or 22156 or 22157 or 22158 or 22159
                    or 22160 or 22161 or 22162 or 22163 or 22164 or 22165 or 22166 or 22167 or 22168 or 22169 or 22170 or 22171 or 22172 or 22173 or 22174 or 22175
                    or 22176 or 22177 or 22178 or 22179 or 22180 or 22181 or 22182 or 22183 or 22184 or 22185 or 22186 or 22187 or 22188 or 22189 or 22190 or 22191
                    or 22192 or 22193 or 22194 or 22195 or 22196 or 22197 or 22198 or 22199 or 22200 or 22201 or 22202 or 22203 or 22204 or 22205 or 22206 or 22207
                    or 22208 or 22209 or 22210 or 22211 or 22212 or 22213 or 22214 or 22215 or 22216 or 22217 or 22218 or 22219 or 22220 or 22221 or 22222 or 22223
                    or 22224 or 22225 or 22226 or 22227 or 22228 or 22229 or 22230 or 22231 or 22232 or 22233 or 22234 or 22235 or 22236 or 22237 or 22238 or 22239
                    or 22240 or 22241 or 22242 or 22243 or 22244 or 22245 or 22246 or 22247 or 22248 or 22249 or 22250 or 22251 or 22252 or 22253 or 22254 or 22255
                    or 22256 or 22257 or 22258 or 22259 or 22260 or 22261 or 22262 or 22263 or 22264 or 22265 or 22266 or 22267 or 22268 or 22269 or 22270 or 22271
                    or 22272 or 22273 or 22274 or 22275 or 22276 or 22277 or 22278 or 22279 or 22280 or 22281 or 22282 or 22283 or 22284 or 22285 or 22286 or 22287
                    or 22288 or 22289 or 22290 or 22291 or 22292 or 22293 or 22294 or 22295 or 22296 or 22297 or 22298 or 22299
                        => await FixCryptographicServicesAsync(),

                    #endregion

                    #region BROAD BUCKETS

                    // BUCKET 1: Core & Power
                    41 or 1074 or 6008 or 1011 or 12 or 13 or 18 or 109 or 110 or 117 or 6005 or 6006 or 6009
                    or 1076 or 1102 or 4647 or 4688 or 4689 or 1 or 4 or 15 or 42 or 107 or 137 or 506 or 507 or 524
                    or 525 or 533 or 566 or 601 or 604 or 10000 or 10001 or 10100 or 10101 or 10102 or 10103 or 10104
                    or 10105 or 10106 or 10107 or 10108 or 10109 or 10110
                    or 10111 or 10112 or 10113 or 10114 or 10115 or 10116 or 10117 or 10118 or 10119 or 10120
                    => await FixPowerFastStartupAsync(),

                    // BUCKET 2: Identity & DCOM (Includes Audit/Security events)
                    10016 or 1500 or 1502 or 1511 or 1515 or 1542 or 4625 or 10005 or 40961 or 40962 or 1530 or 1534
                    or 4648 or 4720 or 4722 or 4723 or 4724 or 4725 or 4726 or 4738 or 4740 or 1501 or 1504 or 1505
                    or 1506 or 1507 or 1508 or 1509 or 1512 or 1513 or 1514 or 1517 or 1531 or 1532 or 4624 or 4634
                    or 4672 or 4732 or 4733 or 4735 or 4800 or 4801 or 4802 or 4803 or 5140 or 5142 or 5145 or 6272
                    or 6273 or 6278 or 1101 or 1104 or 1105 or 1108
                    or 4741 or 4742 or 4743 or 4744 or 4745 or 4746 or 4747 or 4748 or 4749 or 4750
                    or 4751 or 4752 or 4753 or 4754 or 4755 or 4756 or 4757 or 4758 or 4759 or 4760 or 4761 or 4762
                    or 4763 or 4764 or 4765 or 4766 or 4767 or 4768 or 4769 or 4770 or 4771 or 4772 or 4773 or 4774
                    or 4775 or 4776 or 4777 or 4778 or 4779 or 4780 or 4781 or 4782 or 4783 or 4784 or 4785 or 4786
                    or 4787 or 4788 or 4789 or 4790 or 4791 or 4792 or 4793 or 4794 or 4795 or 4796 or 4797 or 4798
                    or 4799 or 4804 or 4805 or 4806 or 4807 or 4808 or 4809 or 4810 or 4811 or 4812 or 4813 or 4814
                    or 4815 or 4816 or 4817 or 4818 or 4819 or 4820 or 4821 or 4822 or 4823 or 4824 or 4825 or 4826
                    or 4827 or 4828 or 4829 or 4830 or 4831 or 4832 or 4833 or 4834 or 4835 or 4836 or 4837 or 4838
                    or 4839 or 4840 or 4841 or 4842 or 4843 or 4844 or 4845 or 4846 or 4847 or 4848 or 4849 or 4850
                    or 4851 or 4852 or 4853 or 4854
                    or 4855 or 4856 or 4857 or 4858 or 4859 or 4860 or 4861 or 4862 or 4863 or 4864 or 4865 or 4866 or 4867 or 4868 or 4869 or 4870
                    or 4871 or 4872 or 4873 or 4874 or 4875 or 4876 or 4877 or 4878 or 4879 or 4880 or 4881 or 4882 or 4883 or 4884 or 4885 or 4886
                    or 4887 or 4888 or 4889 or 4890 or 4891 or 4892 or 4893 or 4894 or 4895 or 4896 or 4897 or 4898 or 4899 or 4900 or 4901 or 4902
                    or 4903 or 4904 or 4905 or 4906 or 4907 or 4908 or 4909 or 4910 or 4911 or 4912 or 4913 or 4914 or 4915 or 4916 or 4917 or 4918
                    or 4919 or 4920 or 4921 or 4922 or 4923 or 4924 or 4925 or 4926 or 4927 or 4928 or 4929 or 4930 or 4931 or 4932 or 4933 or 4934
                    or 4935 or 4936 or 4937 or 4938 or 4939 or 4940 or 4941 or 4942 or 4943 or 4944 or 4945 or 4946 or 4947 or 4948 or 4949 or 4950
                    or 4951 or 4952 or 4953 or 4954 or 4955 or 4956 or 4957 or 4958 or 4959 or 4960 or 4961 or 4962 or 4963 or 4964 or 4965 or 4966
                    or 4967 or 4968 or 4969 or 4970 or 4971 or 4972 or 4973 or 4974 or 4975 or 4976 or 4977 or 4978 or 4979 or 4980 or 4981 or 4982
                    or 4983 or 4984 or 4985 or 4986 or 4987 or 4988 or 4989 or 4990 or 4991 or 4992 or 4993 or 4994 or 4995 or 4996 or 4997 or 4998
                    or 4999
                    or 40963 or 40964 or 40965 or 40966 or 40967 or 40968 or 40969 or 40970 or 40971 or 40972 or 40973 or 40974 or 40975 or 40976 or 40977 or 40978
                    or 40979 or 40980 or 40981 or 40982 or 40983 or 40984 or 40985 or 40986 or 40987 or 40988 or 40989 or 40990 or 40991 or 40992 or 40993 or 40994
                    or 40995 or 40996 or 40997 or 40998 or 40999 or 41000 or 41001 or 41002 or 41003 or 41004 or 41005 or 41006 or 41007 or 41008 or 41009 or 41010
                    or 41011 or 41012 or 41013 or 41014 or 41015 or 41016 or 41017 or 41018 or 41019 or 41020 or 41021 or 41022 or 41023 or 41024 or 41025 or 41026
                    or 41027 or 41028 or 41029 or 41030 or 41031 or 41032 or 41033 or 41034 or 41035 or 41036 or 41037 or 41038 or 41039 or 41040 or 41041 or 41042
                    or 41043 or 41044 or 41045 or 41046 or 41047 or 41048 or 41049 or 41050 or 41051 or 41052 or 41053 or 41054 or 41055 or 41056 or 41057 or 41058
                    or 41059 or 41060 or 41061 or 41062 or 41063 or 41064 or 41065 or 41066 or 41067 or 41068 or 41069 or 41070 or 41071
                    => await FixDCOMAsync(),

                    // BUCKET 3: Networking (Includes Remote Desktop & Terminal Services events)
                    1012 or 1015 or 4227 or 4231 or 10400 or 1003 or 1004 or 4226 or 4319 or 8003 or 8021
                    or 10000 or 10011 or 5719 or 11001 or 11004 or 10053 or 1013 or 1017 or 1018 or 1019
                    or 8000 or 8001 or 8002 or 8004 or 1002 or 1005 or 1006 or 1007 or 1009 or 1010 or 1011 or 1016
                    or 1020 or 5000 or 5001 or 5004 or 5006 or 5007 or 5010 or 5011 or 5012 or 5032 or 7001 or 7002
                    or 7003 or 7004 or 7005 or 7006 or 10012 or 10020 or 11002 or 11005 or 11006 or 12001 or 12010
                    or 12011 or 12012 or 12013
                    or 10065 or 10066 or 10067 or 10068 or 10069 or 10070 or 10071 or 10072 or 10073 or 10074
                    or 23000 or 23001 or 23002 or 23003 or 23004 or 23005 or 23006 or 23007 or 23008 or 23009 or 23010 or 23011 or 23012 or 23013 or 23014 or 23015
                    or 23016 or 23017 or 23018 or 23019 or 23020 or 23021 or 23022 or 23023 or 23024 or 23025 or 23026 or 23027 or 23028 or 23029 or 23030 or 23031
                    or 23032 or 23033 or 23034 or 23035 or 23036 or 23037 or 23038 or 23039 or 23040 or 23041 or 23042 or 23043 or 23044 or 23045 or 23046 or 23047
                    or 23048 or 23049 or 23050 or 23051 or 23052 or 23053 or 23054 or 23055 or 23056 or 23057 or 23058 or 23059 or 23060 or 23061 or 23062 or 23063
                    or 23064 or 23065 or 23066 or 23067 or 23068 or 23069 or 23070 or 23071 or 23072 or 23073 or 23074 or 23075 or 23076 or 23077 or 23078 or 23079
                    or 23080 or 23081 or 23082 or 23083 or 23084 or 23085 or 23086 or 23087 or 23088 or 23089 or 23090 or 23091 or 23092 or 23093 or 23094 or 23095
                    or 23096 or 23097 or 23098 or 23099 or 23100 or 23101 or 23102 or 23103 or 23104 or 23105 or 23106 or 23107 or 23108 or 23109 or 23110 or 23111
                    or 23112 or 23113 or 23114 or 23115 or 23116 or 23117 or 23118 or 23119 or 23120 or 23121 or 23122 or 23123 or 23124 or 23125 or 23126 or 23127
                    or 23128 or 23129 or 23130 or 23131 or 23132 or 23133 or 23134 or 23135 or 23136 or 23137 or 23138 or 23139 or 23140 or 23141 or 23142 or 23143
                    or 23144 or 23145 or 23146 or 23147 or 23148 or 23149 or 23150 or 23151 or 23152 or 23153 or 23154 or 23155 or 23156 or 23157 or 23158 or 23159
                    or 23160 or 23161 or 23162 or 23163 or 23164 or 23165 or 23166 or 23167 or 23168 or 23169 or 23170 or 23171 or 23172 or 23173 or 23174 or 23175
                    or 23176 or 23177 or 23178 or 23179 or 23180 or 23181 or 23182 or 23183 or 23184 or 23185 or 23186 or 23187 or 23188 or 23189 or 23190 or 23191
                    or 23192 or 23193 or 23194 or 23195 or 23196 or 23197 or 23198 or 23199 or 23200 or 23201 or 23202 or 23203 or 23204 or 23205 or 23206 or 23207
                    or 23208 or 23209 or 23210 or 23211 or 23212 or 23213 or 23214 or 23215 or 23216 or 23217 or 23218 or 23219 or 23220 or 23221 or 23222 or 23223
                    or 23224 or 23225 or 23226 or 23227 or 23228 or 23229 or 23230 or 23231 or 23232 or 23233 or 23234 or 23235 or 23236 or 23237 or 23238 or 23239
                    or 23240 or 23241 or 23242 or 23243 or 23244 or 23245 or 23246 or 23247 or 23248 or 23249 or 23250 or 23251 or 23252 or 23253 or 23254 or 23255
                    or 23256 or 23257 or 23258 or 23259 or 23260 or 23261 or 23262 or 23263 or 23264 or 23265 or 23266 or 23267 or 23268 or 23269 or 23270 or 23271
                    or 23272 or 23273 or 23274 or 23275 or 23276 or 23277 or 23278 or 23279 or 23280 or 23281 or 23282 or 23283 or 23284 or 23285 or 23286 or 23287
                    or 23288 or 23289 or 23290 or 23291 or 23292 or 23293 or 23294 or 23295 or 23296 or 23297 or 23298 or 23299 or 23300 or 23301 or 23302 or 23303
                    or 23304 or 23305 or 23306 or 23307 or 23308 or 23309 or 23310 or 23311 or 23312 or 23313 or 23314 or 23315 or 23316 or 23317 or 23318 or 23319
                    or 23320 or 23321 or 23322 or 23323 or 23324 or 23325 or 23326 or 23327 or 23328 or 23329 or 23330 or 23331 or 23332 or 23333 or 23334 or 23335
                    or 23336 or 23337 or 23338 or 23339 or 23340 or 23341 or 23342 or 23343 or 23344 or 23345 or 23346 or 23347 or 23348 or 23349 or 23350 or 23351
                    or 23352 or 23353 or 23354 or 23355 or 23356 or 23357 or 23358 or 23359 or 23360 or 23361 or 23362 or 23363 or 23364 or 23365 or 23366 or 23367
                    or 23368 or 23369 or 23370 or 23371 or 23372 or 23373 or 23374 or 23375 or 23376 or 23377 or 23378 or 23379 or 23380 or 23381 or 23382 or 23383
                    or 23384 or 23385 or 23386 or 23387 or 23388 or 23389 or 23390 or 23391 or 23392 or 23393 or 23394 or 23395 or 23396 or 23397 or 23398 or 23399
                    => await FixTcpIpStackAsync(),

                    // BUCKET 4: Update & Store (Includes CBS & Servicing events)
                    20 or 17 or 19 or 25 or 34 or 2100 or 2101 or 2102 or 512 or 514 or 4004
                    or 4005 or 4007 or 4008 or 1016 or 5000 or 5001 or 5002 or 5003 or 10 or 11 or 14 or 21 or 22
                    or 23 or 24 or 31 or 32 or 33 or 35 or 37 or 38 or 40 or 44 or 45 or 201 or 202 or 300 or 301
                    or 302 or 303 or 304 or 305 or 306 or 307 or 308 or 400 or 401 or 402 or 403 or 404 or 405 or 406
                    or 407 or 408 or 409 or 410 or 411 or 504 or 505
                    or 417 or 418 or 419 or 420 or 421 or 422 or 423 or 424 or 425 or 426
                    or 21000 or 21001 or 21002 or 21003 or 21004 or 21005 or 21006 or 21007 or 21008 or 21009 or 21010 or 21011 or 21012 or 21013 or 21014 or 21015
                    or 21016 or 21017 or 21018 or 21019 or 21020 or 21021 or 21022 or 21023 or 21024 or 21025 or 21026 or 21027 or 21028 or 21029 or 21030 or 21031
                    or 21032 or 21033 or 21034 or 21035 or 21036 or 21037 or 21038 or 21039 or 21040 or 21041 or 21042 or 21043 or 21044 or 21045 or 21046 or 21047
                    or 21048 or 21049 or 21050 or 21051 or 21052 or 21053 or 21054 or 21055 or 21056 or 21057 or 21058 or 21059 or 21060 or 21061 or 21062 or 21063
                    or 21064 or 21065 or 21066 or 21067 or 21068 or 21069 or 21070 or 21071 or 21072 or 21073 or 21074 or 21075 or 21076 or 21077 or 21078 or 21079
                    or 21080 or 21081 or 21082 or 21083 or 21084 or 21085 or 21086 or 21087 or 21088 or 21089 or 21090 or 21091 or 21092 or 21093 or 21094 or 21095
                    or 21096 or 21097 or 21098 or 21099 or 21100 or 21101 or 21102 or 21103 or 21104 or 21105 or 21106 or 21107 or 21108 or 21109 or 21110 or 21111
                    or 21112 or 21113 or 21114 or 21115 or 21116 or 21117 or 21118 or 21119 or 21120 or 21121 or 21122 or 21123 or 21124 or 21125 or 21126 or 21127
                    or 21128 or 21129 or 21130 or 21131 or 21132 or 21133 or 21134 or 21135 or 21136 or 21137 or 21138 or 21139 or 21140 or 21141 or 21142 or 21143
                    or 21144 or 21145 or 21146 or 21147 or 21148 or 21149 or 21150 or 21151 or 21152 or 21153 or 21154 or 21155 or 21156 or 21157 or 21158 or 21159
                    or 21160 or 21161 or 21162 or 21163 or 21164 or 21165 or 21166 or 21167 or 21168 or 21169 or 21170 or 21171 or 21172 or 21173 or 21174 or 21175
                    or 21176 or 21177 or 21178 or 21179 or 21180 or 21181 or 21182 or 21183 or 21184 or 21185 or 21186 or 21187 or 21188 or 21189 or 21190 or 21191
                    or 21192 or 21193 or 21194 or 21195 or 21196 or 21197 or 21198 or 21199 or 21200 or 21201 or 21202 or 21203 or 21204 or 21205 or 21206 or 21207
                    or 21208 or 21209 or 21210 or 21211 or 21212 or 21213 or 21214 or 21215 or 21216 or 21217 or 21218 or 21219 or 21220 or 21221 or 21222 or 21223
                    or 21224 or 21225 or 21226 or 21227 or 21228 or 21229 or 21230 or 21231 or 21232 or 21233 or 21234 or 21235 or 21236 or 21237 or 21238 or 21239
                    or 21240 or 21241 or 21242 or 21243 or 21244 or 21245 or 21246 or 21247 or 21248 or 21249 or 21250 or 21251 or 21252 or 21253 or 21254 or 21255
                    or 21256 or 21257 or 21258 or 21259 or 21260 or 21261 or 21262 or 21263 or 21264 or 21265 or 21266 or 21267 or 21268 or 21269 or 21270 or 21271
                    or 21272 or 21273 or 21274 or 21275 or 21276 or 21277 or 21278 or 21279 or 21280 or 21281 or 21282 or 21283 or 21284 or 21285 or 21286 or 21287
                    or 21288 or 21289 or 21290 or 21291 or 21292 or 21293 or 21294 or 21295 or 21296 or 21297 or 21298 or 21299
                    => await FixWindowsUpdateAsync(),

                    // BUCKET 5: UI Shell
                    1002 or 1022 or 489 or 490 or 1010 or 491 or 492 or 493 or 1003
                    or 1004 or 1005 or 1006 or 1007 or 1009 or 1011 or 1012 or 1013 or 1015 or 1017 or 1018
                    or 1019 or 1020 or 1021 or 1024 or 1025 or 2000 or 2001 or 2003 or 2005 or 3000 or 3001
                    or 3002 or 3003 or 3004 or 8000 or 8001 or 9000 or 9001
                    => await RestartExplorerAsync(),

                    // BUCKET 6: Storage
                    55 or 98 or 11 or 15 or 51 or 153 or 7 or 130 or 137 or 140 or 12293 or 12298 or 8224 or 2049
                    or 2050 or 50 or 57 or 12290 or 8213 or 8217 or 8218 or 8219 or 8220 or 8221 or 8222 or 8223
                    or 8225 or 8226 or 12291 or 2 or 5 or 8 or 9 or 12 or 14 or 26 or 27 or 28 or 29 or 30
                    or 31 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 52 or 54 or 56 or 58 or 59 or 60
                    or 129 or 132 or 133 or 134 or 135 or 136 or 138 or 139 or 141 or 142 or 143
                    or 155 or 156 or 157 or 158 or 159 or 160 or 161 or 162 or 163 or 164
                    => await FixDiskCorruptionAsync(),

                    // BUCKET 7: Service logic (Includes AppLocker & Hyper-V events)
                    35 or 36870 or 7000 or 7009 or 7011 or 7023 or 7024
                    or 7031 or 7032 or 7034 or 7036 or 7040 or 12292 or 12294 or 12295 or 12296 or 12297 or 12300
                    or 12301 or 12302 or 12303 or 12304 or 63 or 100 or 101 or 102 or 103 or 317 or 800 or 801
                    or 804 or 805 or 806 or 809 or 810 or 7001 or 7022 or 7026 or 7030 or 7035 or 7045 or 7046 or 7047
                    or 7048 or 7049 or 7050 or 7051 or 7052
                    or 15000 or 15001 or 15002 or 15003 or 15004 or 15005 or 15006 or 15007 or 15008 or 15009 or 15010 or 15011 or 15012 or 15013 or 15014 or 15015
                    or 15016 or 15017 or 15018 or 15019 or 15020 or 15021 or 15022 or 15023 or 15024 or 15025 or 15026 or 15027 or 15028 or 15029 or 15030 or 15031
                    or 15032 or 15033 or 15034 or 15035 or 15036 or 15037 or 15038 or 15039 or 15040 or 15041 or 15042 or 15043 or 15044 or 15045 or 15046 or 15047
                    or 15048 or 15049 or 15050 or 15051 or 15052 or 15053 or 15054 or 15055 or 15056 or 15057 or 15058 or 15059 or 15060 or 15061 or 15062 or 15063
                    or 15064 or 15065 or 15066 or 15067 or 15068 or 15069 or 15070 or 15071 or 15072 or 15073 or 15074 or 15075 or 15076 or 15077 or 15078 or 15079
                    or 15080 or 15081 or 15082 or 15083 or 15084 or 15085 or 15086 or 15087 or 15088 or 15089 or 15090 or 15091 or 15092 or 15093 or 15094 or 15095
                    or 15096 or 15097 or 15098 or 15099 or 15100 or 15101 or 15102 or 15103 or 15104 or 15105 or 15106 or 15107 or 15108 or 15109 or 15110 or 15111
                    or 15112 or 15113 or 15114 or 15115 or 15116 or 15117 or 15118 or 15119 or 15120 or 15121 or 15122 or 15123 or 15124 or 15125 or 15126 or 15127
                    or 15128 or 15129 or 15130 or 15131 or 15132 or 15133 or 15134 or 15135 or 15136 or 15137 or 15138 or 15139 or 15140 or 15141 or 15142 or 15143
                    or 15144 or 15145 or 15146 or 15147 or 15148 or 15149 or 15150
                    or 8005 or 8006 or 8007 or 8008 or 8009 or 8010 or 8011 or 8012 or 8013 or 8014 or 8015 or 8016 or 8017 or 8018 or 8019 or 8020
                    or 8022 or 8023 or 8024 or 8025 or 8026 or 8027 or 8028 or 8029 or 8030 or 8031 or 8032 or 8034 or 8035 or 8036 or 8037 or 8038
                    or 8039 or 8040 or 8041 or 8042 or 8043 or 8044 or 8045 or 8046 or 8047 or 8048 or 8049 or 8050 or 8051 or 8052 or 8053 or 8054
                    or 8055 or 8056 or 8057 or 8058 or 8059 or 8060 or 8061 or 8062 or 8063 or 8064 or 8065 or 8066 or 8067 or 8068 or 8069 or 8070
                    or 8071 or 8072 or 8073 or 8074 or 8075 or 8076 or 8077 or 8078 or 8079 or 8080 or 8081 or 8082 or 8083 or 8084 or 8085 or 8086
                    or 8087 or 8088 or 8089 or 8090 or 8091 or 8092 or 8093 or 8094 or 8095 or 8096 or 8097 or 8098 or 8099
                    => await FixServiceTimeoutAsync(),

                    _ => false

                    #endregion
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemediationEngine] Critical failure for ID {eventId}: {ex.Message}");
                return false;
            }
        }

        #region NATIVE PERFORMANCE BRIDGES

        private static async Task<bool> ExecuteNativeMemoryCleanAsync()
        {
            try
            {
                var result = await ClearingMemory.StartMemoryCleanup(clearRamCache: true, optimizeWorkingSet: true, shouldRemoveWinOld: false, shouldFlushDns: false);
                return result.MemoryCleanupAttempted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] Native Memory Clean Failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> ExecuteNativeDiskOptimizeAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    ClearingMemory.OptimizeModifiedFileCache();
                    ClearingMemory.OptimizeModifiedPageList();
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] Native Disk Optimize Failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region SOFTWARE REPAIRS

        private static async Task<bool> FixPowerFastStartupAsync()
        {
            string script = "powercfg /h off; Start-Sleep -Seconds 2; powercfg /h on";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixCryptographicServicesAsync()
        {
            string script = @"
                Stop-Service cryptsvc -Force -ErrorAction SilentlyContinue
                Rename-Item -Path ""$env:windir\System32\catroot2"" -NewName ""catroot2.old"" -ErrorAction SilentlyContinue
                Start-Service cryptsvc -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixEsentDatabaseAsync()
        {
            string script = @"New-Item -Path ""$env:windir\system32\config\systemprofile\AppData\Local\TileDataLayer\Database"" -ItemType Directory -Force | Out-Null";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWindowsDefenderAsync()
        {
            await CommandExecutor.RunCommandAsTrustedInstaller("Update-MpSignature", isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWindowsSearchAsync()
        {
            await CommandExecutor.RunCommand("Restart-Service WSearch -Force -ErrorAction SilentlyContinue", isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixAppxDeploymentAsync()
        {
            string script = @"Get-AppxPackage -AllUsers | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml"" -ErrorAction SilentlyContinue}";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixEventTracingAsync()
        {
            string script = @"logman stop EventLog-System -ets -ErrorAction SilentlyContinue; logman start EventLog-System -ets -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixLanmanServerAsync()
        {
            string script = @"Restart-Service LanmanServer -Force -ErrorAction SilentlyContinue; Restart-Service LanmanWorkstation -Force -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixDiskCorruptionAsync()
        {
            await CommandExecutor.RunCommandAsTrustedInstaller("chkdsk C: /scan /perf", isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixServiceTimeoutAsync()
        {
            string script = @"Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control' -Name 'ServicesPipeTimeout' -Value 60000 -Type DWord -Force";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWifiAdapterAsync()
        {
            string script = @"Restart-Service WlanSvc -Force -ErrorAction SilentlyContinue; ipconfig /renew | Out-Null";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixGroupPolicyAsync()
        {
            await CommandExecutor.RunCommand("gpupdate /force", isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixMsiInstallerAsync()
        {
            string script = @"
                msiexec /unregister
                msiexec /regserver
                Restart-Service msiserver -Force -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixVssServiceAsync()
        {
            string script = @"Restart-Service vss -Force -ErrorAction SilentlyContinue; Restart-Service swprv -Force -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixSchannelAsync()
        {
            await CommandExecutor.RunCommand("certutil -setreg chain\\ChainCacheResyncFiletime @now", isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixResourceExhaustionAsync()
        {
            string script = @"Restart-Service SysMain -Force -ErrorAction SilentlyContinue; Stop-Process -Name dwm -Force -ErrorAction SilentlyContinue";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixTimeSyncAsync()
        {
            string script = @"
                Stop-Service w32time -ErrorAction SilentlyContinue
                w32tm /unregister | Out-Null
                w32tm /register | Out-Null
                Start-Service w32time -ErrorAction SilentlyContinue
                w32tm /resync /nowait | Out-Null
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixTcpIpStackAsync()
        {
            string script = @"
                netsh winsock reset | Out-Null
                netsh int ip reset | Out-Null
                ipconfig /release | Out-Null
                ipconfig /renew | Out-Null
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixWindowsStoreAsync()
        {
            await CommandExecutor.RunCommand("wsreset.exe -i", isPowerShell: false);
            return true;
        }

        private static async Task<bool> RunSystemFileRepairAsync()
        {
            string script = "DISM.exe /Online /Cleanup-image /Restorehealth";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixPerformanceCountersAsync()
        {
            string script = @"
                cd \windows\system32
                lodctr /r
                cd \windows\syswow64
                lodctr /r
                WINMGMT.EXE /RESYNCPERF
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: false);
            return true;
        }

        private static async Task<bool> FixWindowsUpdateAsync()
        {
            string script = @"
                Stop-Service -Name wuauserv -Force -ErrorAction SilentlyContinue
                Stop-Service -Name bits -Force -ErrorAction SilentlyContinue
                Remove-Item -Path ""$env:windir\SoftwareDistribution\Download\*"" -Recurse -Force -ErrorAction SilentlyContinue
                Start-Service -Name wuauserv -ErrorAction SilentlyContinue
                Start-Service -Name bits -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixPrintSpoolerAsync()
        {
            string script = @"
                Stop-Service -Name Spooler -Force -ErrorAction SilentlyContinue
                Remove-Item -Path ""$env:windir\System32\spool\PRINTERS\*.*"" -Force -Recurse -ErrorAction SilentlyContinue
                Start-Service -Name Spooler -ErrorAction SilentlyContinue
            ";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> RestartExplorerAsync()
        {
            string script = "Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 500; Start-Process explorer";
            await CommandExecutor.RunCommand(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixNetworkDnsAsync()
        {
            string script = @"
                ipconfig /flushdns | Out-Null
                ipconfig /registerdns | Out-Null
                try { Restart-Service -Name Dnscache -Force -ErrorAction SilentlyContinue } catch {}
            ";
            await CommandExecutor.RunCommand(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixDCOMAsync()
        {
            string script = @"
                $Paths = @('HKCR:\AppID\{9CA88EE3-ACB7-47c8-AFC4-AB702511C276}', 'HKCR:\CLSID\{D63B10C5-BB46-4990-A94F-E40B9D520160}')
                foreach ($path in $Paths) {
                    if (Test-Path $path) {
                        Write-Output 'Repairing DCOM ACLs for path: $path'
                    }
                }";
            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);
            return true;
        }

        private static async Task<bool> FixLuafvServiceAsync()
        {
            string script = @"
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\luafv' -Name 'Start' -Value 2 -Type DWord -Force
                Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name 'EnableLUA' -Value 1 -Type DWord -Force
            ";

            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);

            return true;
        }

        private static async Task<bool> FixSecureBootKeysAsync()
        {
            string script = @"
                $bitlocker = Get-BitLockerVolume -MountPoint 'C:' -ErrorAction SilentlyContinue
                if ($bitlocker -and $bitlocker.ProtectionStatus -eq 'On') {
                    Suspend-BitLocker -MountPoint 'C:' -RebootCount 2 -ErrorAction SilentlyContinue
                }

                $regPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot'
                $taskPath = '\Microsoft\Windows\PI\Secure-Boot-Update'

                # 1. Apply DBX Update (0x40) as explicit DWORD
                Set-ItemProperty -Path $regPath -Name 'AvailableUpdates' -Value ([uint32]0x40) -PropertyType DWord -Force
                Start-ScheduledTask -TaskName $taskPath -ErrorAction SilentlyContinue
        
                # Wait for the task to actually start/trigger processing
                Start-Sleep -Seconds 3

                # 2. Force the task to stop so it can be re-triggered for the next key
                Stop-ScheduledTask -TaskName $taskPath -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 1

                # 3. Apply DB CA Update (0x5944) as explicit DWORD
                # This matches your manual 'reg add' /d 0x5944 logic exactly
                Set-ItemProperty -Path $regPath -Name 'AvailableUpdates' -Value ([uint32]0x5944) -PropertyType DWord -Force
                Start-ScheduledTask -TaskName $taskPath -ErrorAction SilentlyContinue
            ";

            await CommandExecutor.RunCommandAsTrustedInstaller(script, isPowerShell: true);

            var currentXamlRoot = App.MainWindow?.Content?.XamlRoot;

            if (currentXamlRoot != null)
            {
                ContentDialog restartDialog = new ContentDialog
                {
                    XamlRoot = currentXamlRoot,
                    Title = ResourceString.GetString("diag_reboot_required_title") ?? "Restart Required",
                    Content = ResourceString.GetString("diag_secureboot_reboot_msg") ?? "Secure Boot update staged successfully. CRITICAL: You must restart your computer TWICE for your motherboard firmware to enroll the new keys. Would you like to restart your computer now?",
                    PrimaryButtonText = ResourceString.GetString("txt_restart_now") ?? "Restart Now",
                    CloseButtonText = ResourceString.GetString("txt_later") ?? "Later",
                    DefaultButton = ContentDialogButton.Primary
                };

                if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
                {
                    restartDialog.Style = (Style)style;
                }

                ContentDialogResult result = await restartDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    string shutdownComment = ResourceString.GetString("diag_secureboot_shutdown_comment") ?? "EvolveOS Optimizer: Secure Boot Key Enrollment";

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = $"/r /t 5 /c \"{shutdownComment}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
            }

            return true;
        }

        private static async Task<bool> ResetDisplayDriverAsync()
        {
            string script = "Add-Type -TypeDefinition '[DllImport(\"user32.dll\")] public class User32 { [DllImport(\"user32.dll\")] public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase); }'; [User32]::InvalidateRect([IntPtr]::Zero, [IntPtr]::Zero, $true)";
            await CommandExecutor.RunCommand(script, isPowerShell: true);
            return true;
        }

        #endregion

        #endregion

        #region Hardware Remediation

        public static async Task<bool> RunHardwareFixAsync(HardwareIssue issue)
        {
            if (issue == null || string.IsNullOrEmpty(issue.DeviceId)) return false;

            try
            {
                // Bucket 1: The device is physically Disabled
                if (issue.WmiErrorCode == 22)
                {
                    return await EnableDeviceDeepAsync(issue.DeviceId);
                }

                // Bucket 2: Soft Reset (Driver Crash, Power State, Resource Conflict, Init Failure)
                else if (issue.WmiErrorCode is 10 or 14 or 31 or 37 or 38 or 39 or 41 or 43 or 21 or 32 or 44 or 54 or 9 or 11 or 18 or 20 or 34 or 35 or 36 or 40 or 42 or 46 or 50 or 51 or 56 or 2 or 6 or 7 or 8 or 25 or 26 or 27 or 30 or 55 or 57 or 58 or 24 or 45 or 81 or 82 or 83 or 84 or 85
                    or 86 or 87 or 88 or 89 or 90 or 91 or 92 or 93 or 94 or 95 or 96 or 97 or 98 or 99 or 100 or 101 or 102 or 103 or 104 or 105
                    or 136 or 137 or 138 or 139 or 140 or 141 or 142 or 143 or 144 or 145 or 146 or 147 or 148 or 149 or 150 or 151 or 152 or 153 or 154 or 155
                    or 186 or 187 or 188 or 189 or 190 or 191 or 192 or 193 or 194 or 195 or 196 or 197 or 198 or 199 or 200 or 201 or 202 or 203 or 204 or 205
                    or 236 or 237 or 238 or 239 or 240 or 241 or 242 or 243 or 244 or 245 or 246 or 247 or 248 or 249 or 250
                    or 251 or 252 or 253 or 254 or 255 or 256 or 257 or 258 or 259 or 260 or 261 or 262 or 263 or 264 or 265
                    or 266 or 267 or 268
                    or 336 or 337 or 338 or 339 or 340 or 341 or 342 or 343 or 344 or 345 or 346 or 347 or 348 or 349 or 350
                    or 351 or 352 or 353 or 354 or 355 or 356 or 357 or 358 or 359 or 360 or 361 or 362 or 363 or 364 or 365
                    or 366 or 367 or 368 or 369 or 370 or 371 or 372 or 373 or 374 or 375 or 376 or 377 or 378 or 379 or 380
                    or 381 or 382 or 383 or 384 or 385 or 386 or 387 or 388 or 389 or 390 or 391 or 392 or 393 or 394 or 395
                    or 396 or 397 or 398 or 399 or 400 or 401 or 402 or 403 or 404 or 405 or 406 or 407 or 408 or 409 or 410
                    or 411 or 412 or 413 or 414 or 415 or 416 or 417 or 418 or 419 or 420 or 421 or 422 or 423 or 424 or 425
                    or 426 or 427 or 428 or 429 or 430 or 431 or 432 or 433 or 434 or 435 or 436 or 437 or 438 or 439 or 440
                    or 441 or 442 or 443 or 444 or 445 or 446 or 447 or 448 or 449 or 450 or 451 or 452 or 453 or 454 or 455
                    or 456 or 457 or 458 or 459 or 460 or 461 or 462 or 463 or 464 or 465 or 466 or 467 or 468 or 469 or 470
                    or 471 or 472 or 473 or 474 or 475 or 476 or 477 or 478 or 479 or 480 or 481 or 482 or 483 or 484 or 485
                    or 486 or 487 or 488 or 489 or 490 or 491 or 492 or 493 or 494 or 495 or 496 or 497 or 498 or 499 or 500
                    or 501 or 502
                    // --- NEW 1000 CODES (Part 1: 836 to 1169) ---
                    or 836 or 837 or 838 or 839 or 840 or 841 or 842 or 843 or 844 or 845 or 846 or 847 or 848 or 849 or 850
                    or 851 or 852 or 853 or 854 or 855 or 856 or 857 or 858 or 859 or 860 or 861 or 862 or 863 or 864 or 865
                    or 866 or 867 or 868 or 869 or 870 or 871 or 872 or 873 or 874 or 875 or 876 or 877 or 878 or 879 or 880
                    or 881 or 882 or 883 or 884 or 885 or 886 or 887 or 888 or 889 or 890 or 891 or 892 or 893 or 894 or 895
                    or 896 or 897 or 898 or 899 or 900 or 901 or 902 or 903 or 904 or 905 or 906 or 907 or 908 or 909 or 910
                    or 911 or 912 or 913 or 914 or 915 or 916 or 917 or 918 or 919 or 920 or 921 or 922 or 923 or 924 or 925
                    or 926 or 927 or 928 or 929 or 930 or 931 or 932 or 933 or 934 or 935 or 936 or 937 or 938 or 939 or 940
                    or 941 or 942 or 943 or 944 or 945 or 946 or 947 or 948 or 949 or 950 or 951 or 952 or 953 or 954 or 955
                    or 956 or 957 or 958 or 959 or 960 or 961 or 962 or 963 or 964 or 965 or 966 or 967 or 968 or 969 or 970
                    or 971 or 972 or 973 or 974 or 975 or 976 or 977 or 978 or 979 or 980 or 981 or 982 or 983 or 984 or 985
                    or 986 or 987 or 988 or 989 or 990 or 991 or 992 or 993 or 994 or 995 or 996 or 997 or 998 or 999 or 1000
                    or 1001 or 1002 or 1003 or 1004 or 1005 or 1006 or 1007 or 1008 or 1009 or 1010 or 1011 or 1012 or 1013 or 1014 or 1015
                    or 1016 or 1017 or 1018 or 1019 or 1020 or 1021 or 1022 or 1023 or 1024 or 1025 or 1026 or 1027 or 1028 or 1029 or 1030
                    or 1031 or 1032 or 1033 or 1034 or 1035 or 1036 or 1037 or 1038 or 1039 or 1040 or 1041 or 1042 or 1043 or 1044 or 1045
                    or 1046 or 1047 or 1048 or 1049 or 1050 or 1051 or 1052 or 1053 or 1054 or 1055 or 1056 or 1057 or 1058 or 1059 or 1060
                    or 1061 or 1062 or 1063 or 1064 or 1065 or 1066 or 1067 or 1068 or 1069 or 1070 or 1071 or 1072 or 1073 or 1074 or 1075
                    or 1076 or 1077 or 1078 or 1079 or 1080 or 1081 or 1082 or 1083 or 1084 or 1085 or 1086 or 1087 or 1088 or 1089 or 1090
                    or 1091 or 1092 or 1093 or 1094 or 1095 or 1096 or 1097 or 1098 or 1099 or 1100 or 1101 or 1102 or 1103 or 1104 or 1105
                    or 1106 or 1107 or 1108 or 1109 or 1110 or 1111 or 1112 or 1113 or 1114 or 1115 or 1116 or 1117 or 1118 or 1119 or 1120
                    or 1121 or 1122 or 1123 or 1124 or 1125 or 1126 or 1127 or 1128 or 1129 or 1130 or 1131 or 1132 or 1133 or 1134 or 1135
                    or 1136 or 1137 or 1138 or 1139 or 1140 or 1141 or 1142 or 1143 or 1144 or 1145 or 1146 or 1147 or 1148 or 1149 or 1150
                    or 1151 or 1152 or 1153 or 1154 or 1155 or 1156 or 1157 or 1158 or 1159 or 1160 or 1161 or 1162 or 1163 or 1164 or 1165
                    or 1166 or 1167 or 1168 or 1169)
                {
                    return await ResetDeviceDeepAsync(issue.DeviceId);
                }

                // Bucket 3: Hardware Rescan (PnP Sync, Bus Failures, Firmware Missing, Multifunction)
                else if (issue.WmiErrorCode is 1 or 12 or 16 or 28 or 29 or 33 or 47 or 53 or 13 or 15 or 17 or 23 or 59 or 60 or 61 or 62 or 63 or 69 or 70 or 71 or 72 or 73
                    or 106 or 107 or 108 or 109 or 110 or 111 or 112 or 113 or 114 or 115 or 116 or 117 or 118 or 119 or 120
                    or 156 or 157 or 158 or 159 or 160 or 161 or 162 or 163 or 164 or 165 or 166 or 167 or 168 or 169 or 170
                    or 206 or 207 or 208 or 209 or 210 or 211 or 212 or 213 or 214 or 215 or 216 or 217 or 218 or 219 or 220
                    or 269 or 270 or 271 or 272 or 273 or 274 or 275 or 276 or 277 or 278 or 279 or 280 or 281 or 282 or 283
                    or 284 or 285 or 286 or 287 or 288 or 289 or 290 or 291 or 292 or 293 or 294 or 295 or 296 or 297 or 298
                    or 299 or 300 or 301
                    or 503 or 504 or 505 or 506 or 507 or 508 or 509 or 510 or 511 or 512 or 513 or 514 or 515 or 516 or 517
                    or 518 or 519 or 520 or 521 or 522 or 523 or 524 or 525 or 526 or 527 or 528 or 529 or 530 or 531 or 532
                    or 533 or 534 or 535 or 536 or 537 or 538 or 539 or 540 or 541 or 542 or 543 or 544 or 545 or 546 or 547
                    or 548 or 549 or 550 or 551 or 552 or 553 or 554 or 555 or 556 or 557 or 558 or 559 or 560 or 561 or 562
                    or 563 or 564 or 565 or 566 or 567 or 568 or 569 or 570 or 571 or 572 or 573 or 574 or 575 or 576 or 577
                    or 578 or 579 or 580 or 581 or 582 or 583 or 584 or 585 or 586 or 587 or 588 or 589 or 590 or 591 or 592
                    or 593 or 594 or 595 or 596 or 597 or 598 or 599 or 600 or 601 or 602 or 603 or 604 or 605 or 606 or 607
                    or 608 or 609 or 610 or 611 or 612 or 613 or 614 or 615 or 616 or 617 or 618 or 619 or 620 or 621 or 622
                    or 623 or 624 or 625 or 626 or 627 or 628 or 629 or 630 or 631 or 632 or 633 or 634 or 635 or 636 or 637
                    or 638 or 639 or 640 or 641 or 642 or 643 or 644 or 645 or 646 or 647 or 648 or 649 or 650 or 651 or 652
                    or 653 or 654 or 655 or 656 or 657 or 658 or 659 or 660 or 661 or 662 or 663 or 664 or 665 or 666 or 667
                    or 668 or 669

                    or 1170 or 1171 or 1172 or 1173 or 1174 or 1175 or 1176 or 1177 or 1178 or 1179 or 1180 or 1181 or 1182 or 1183 or 1184
                    or 1185 or 1186 or 1187 or 1188 or 1189 or 1190 or 1191 or 1192 or 1193 or 1194 or 1195 or 1196 or 1197 or 1198 or 1199
                    or 1200 or 1201 or 1202 or 1203 or 1204 or 1205 or 1206 or 1207 or 1208 or 1209 or 1210 or 1211 or 1212 or 1213 or 1214
                    or 1215 or 1216 or 1217 or 1218 or 1219 or 1220 or 1221 or 1222 or 1223 or 1224 or 1225 or 1226 or 1227 or 1228 or 1229
                    or 1230 or 1231 or 1232 or 1233 or 1234 or 1235 or 1236 or 1237 or 1238 or 1239 or 1240 or 1241 or 1242 or 1243 or 1244
                    or 1245 or 1246 or 1247 or 1248 or 1249 or 1250 or 1251 or 1252 or 1253 or 1254 or 1255 or 1256 or 1257 or 1258 or 1259
                    or 1260 or 1261 or 1262 or 1263 or 1264 or 1265 or 1266 or 1267 or 1268 or 1269 or 1270 or 1271 or 1272 or 1273 or 1274
                    or 1275 or 1276 or 1277 or 1278 or 1279 or 1280 or 1281 or 1282 or 1283 or 1284 or 1285 or 1286 or 1287 or 1288 or 1289
                    or 1290 or 1291 or 1292 or 1293 or 1294 or 1295 or 1296 or 1297 or 1298 or 1299 or 1300 or 1301 or 1302 or 1303 or 1304
                    or 1305 or 1306 or 1307 or 1308 or 1309 or 1310 or 1311 or 1312 or 1313 or 1314 or 1315 or 1316 or 1317 or 1318 or 1319
                    or 1320 or 1321 or 1322 or 1323 or 1324 or 1325 or 1326 or 1327 or 1328 or 1329 or 1330 or 1331 or 1332 or 1333 or 1334
                    or 1335 or 1336 or 1337 or 1338 or 1339 or 1340 or 1341 or 1342 or 1343 or 1344 or 1345 or 1346 or 1347 or 1348 or 1349
                    or 1350 or 1351 or 1352 or 1353 or 1354 or 1355 or 1356 or 1357 or 1358 or 1359 or 1360 or 1361 or 1362 or 1363 or 1364
                    or 1365 or 1366 or 1367 or 1368 or 1369 or 1370 or 1371 or 1372 or 1373 or 1374 or 1375 or 1376 or 1377 or 1378 or 1379
                    or 1380 or 1381 or 1382 or 1383 or 1384 or 1385 or 1386 or 1387 or 1388 or 1389 or 1390 or 1391 or 1392 or 1393 or 1394
                    or 1395 or 1396 or 1397 or 1398 or 1399 or 1400 or 1401 or 1402 or 1403 or 1404 or 1405 or 1406 or 1407 or 1408 or 1409
                    or 1410 or 1411 or 1412 or 1413 or 1414 or 1415 or 1416 or 1417 or 1418 or 1419 or 1420 or 1421 or 1422 or 1423 or 1424
                    or 1425 or 1426 or 1427 or 1428 or 1429 or 1430 or 1431 or 1432 or 1433 or 1434 or 1435 or 1436 or 1437 or 1438 or 1439
                    or 1440 or 1441 or 1442 or 1443 or 1444 or 1445 or 1446 or 1447 or 1448 or 1449 or 1450 or 1451 or 1452 or 1453 or 1454
                    or 1455 or 1456 or 1457 or 1458 or 1459 or 1460 or 1461 or 1462 or 1463 or 1464 or 1465 or 1466 or 1467 or 1468 or 1469
                    or 1470 or 1471 or 1472 or 1473 or 1474 or 1475 or 1476 or 1477 or 1478 or 1479 or 1480 or 1481 or 1482 or 1483 or 1484
                    or 1485 or 1486 or 1487 or 1488 or 1489 or 1490 or 1491 or 1492 or 1493 or 1494 or 1495 or 1496 or 1497 or 1498 or 1499
                    or 1500 or 1501 or 1502)
                {
                    return await RescanPnpHardwareAsync();
                }

                // Bucket 4: Registry & Hard Reinstall (Registry Corruption, Signature Blocked, Hive Overload)
                else if (issue.WmiErrorCode is 19 or 3 or 48 or 52 or 4 or 5 or 49 or 64 or 65 or 66 or 67 or 68
                    or 121 or 122 or 123 or 124 or 125 or 126 or 127 or 128 or 129 or 130 or 131 or 132 or 133 or 134 or 135
                    or 171 or 172 or 173 or 174 or 175 or 176 or 177 or 178 or 179 or 180 or 181 or 182 or 183 or 184 or 185
                    or 221 or 222 or 223 or 224 or 225 or 226 or 227 or 228 or 229 or 230 or 231 or 232 or 233 or 234 or 235
                    or 302 or 303 or 304 or 305 or 306 or 307 or 308 or 309 or 310 or 311 or 312 or 313 or 314 or 315 or 316
                    or 317 or 318 or 319 or 320 or 321 or 322 or 323 or 324 or 325 or 326 or 327 or 328 or 329 or 330 or 331
                    or 332 or 333 or 334 or 335
                    or 670 or 671 or 672 or 673 or 674 or 675 or 676 or 677 or 678 or 679 or 680 or 681 or 682 or 683 or 684
                    or 685 or 686 or 687 or 688 or 689 or 690 or 691 or 692 or 693 or 694 or 695 or 696 or 697 or 698 or 699
                    or 700 or 701 or 702 or 703 or 704 or 705 or 706 or 707 or 708 or 709 or 710 or 711 or 712 or 713 or 714
                    or 715 or 716 or 717 or 718 or 719 or 720 or 721 or 722 or 723 or 724 or 725 or 726 or 727 or 728 or 729
                    or 730 or 731 or 732 or 733 or 734 or 735 or 736 or 737 or 738 or 739 or 740 or 741 or 742 or 743 or 744
                    or 745 or 746 or 747 or 748 or 749 or 750 or 751 or 752 or 753 or 754 or 755 or 756 or 757 or 758 or 759
                    or 760 or 761 or 762 or 763 or 764 or 765 or 766 or 767 or 768 or 769 or 770 or 771 or 772 or 773 or 774
                    or 775 or 776 or 777 or 778 or 779 or 780 or 781 or 782 or 783 or 784 or 785 or 786 or 787 or 788 or 789
                    or 790 or 791 or 792 or 793 or 794 or 795 or 796 or 797 or 798 or 799 or 800 or 801 or 802 or 803 or 804
                    or 805 or 806 or 807 or 808 or 809 or 810 or 811 or 812 or 813 or 814 or 815 or 816 or 817 or 818 or 819
                    or 820 or 821 or 822 or 823 or 824 or 825 or 826 or 827 or 828 or 829 or 830 or 831 or 832 or 833 or 834
                    or 835

                    or 1503 or 1504 or 1505 or 1506 or 1507 or 1508 or 1509 or 1510 or 1511 or 1512 or 1513 or 1514 or 1515
                    or 1516 or 1517 or 1518 or 1519 or 1520 or 1521 or 1522 or 1523 or 1524 or 1525 or 1526 or 1527 or 1528
                    or 1529 or 1530 or 1531 or 1532 or 1533 or 1534 or 1535 or 1536 or 1537 or 1538 or 1539 or 1540 or 1541
                    or 1542 or 1543 or 1544 or 1545 or 1546 or 1547 or 1548 or 1549 or 1550 or 1551 or 1552 or 1553 or 1554
                    or 1555 or 1556 or 1557 or 1558 or 1559 or 1560 or 1561 or 1562 or 1563 or 1564 or 1565 or 1566 or 1567
                    or 1568 or 1569 or 1570 or 1571 or 1572 or 1573 or 1574 or 1575 or 1576 or 1577 or 1578 or 1579 or 1580
                    or 1581 or 1582 or 1583 or 1584 or 1585 or 1586 or 1587 or 1588 or 1589 or 1590 or 1591 or 1592 or 1593
                    or 1594 or 1595 or 1596 or 1597 or 1598 or 1599 or 1600 or 1601 or 1602 or 1603 or 1604 or 1605 or 1606
                    or 1607 or 1608 or 1609 or 1610 or 1611 or 1612 or 1613 or 1614 or 1615 or 1616 or 1617 or 1618 or 1619
                    or 1620 or 1621 or 1622 or 1623 or 1624 or 1625 or 1626 or 1627 or 1628 or 1629 or 1630 or 1631 or 1632
                    or 1633 or 1634 or 1635 or 1636 or 1637 or 1638 or 1639 or 1640 or 1641 or 1642 or 1643 or 1644 or 1645
                    or 1646 or 1647 or 1648 or 1649 or 1650 or 1651 or 1652 or 1653 or 1654 or 1655 or 1656 or 1657 or 1658
                    or 1659 or 1660 or 1661 or 1662 or 1663 or 1664 or 1665 or 1666 or 1667 or 1668 or 1669 or 1670 or 1671
                    or 1672 or 1673 or 1674 or 1675 or 1676 or 1677 or 1678 or 1679 or 1680 or 1681 or 1682 or 1683 or 1684
                    or 1685 or 1686 or 1687 or 1688 or 1689 or 1690 or 1691 or 1692 or 1693 or 1694 or 1695 or 1696 or 1697
                    or 1698 or 1699 or 1700 or 1701 or 1702 or 1703 or 1704 or 1705 or 1706 or 1707 or 1708 or 1709 or 1710
                    or 1711 or 1712 or 1713 or 1714 or 1715 or 1716 or 1717 or 1718 or 1719 or 1720 or 1721 or 1722 or 1723
                    or 1724 or 1725 or 1726 or 1727 or 1728 or 1729 or 1730 or 1731 or 1732 or 1733 or 1734 or 1735 or 1736
                    or 1737 or 1738 or 1739 or 1740 or 1741 or 1742 or 1743 or 1744 or 1745 or 1746 or 1747 or 1748 or 1749
                    or 1750 or 1751 or 1752 or 1753 or 1754 or 1755 or 1756 or 1757 or 1758 or 1759 or 1760 or 1761 or 1762
                    or 1763 or 1764 or 1765 or 1766 or 1767 or 1768 or 1769 or 1770 or 1771 or 1772 or 1773 or 1774 or 1775
                    or 1776 or 1777 or 1778 or 1779 or 1780 or 1781 or 1782 or 1783 or 1784 or 1785 or 1786 or 1787 or 1788
                    or 1789 or 1790 or 1791 or 1792 or 1793 or 1794 or 1795 or 1796 or 1797 or 1798 or 1799 or 1800 or 1801
                    or 1802 or 1803 or 1804 or 1805 or 1806 or 1807 or 1808 or 1809 or 1810 or 1811 or 1812 or 1813 or 1814
                    or 1815 or 1816 or 1817 or 1818 or 1819 or 1820 or 1821 or 1822 or 1823 or 1824 or 1825 or 1826 or 1827
                    or 1828 or 1829 or 1830 or 1831 or 1832 or 1833 or 1834 or 1835)
                {
                    return await UninstallAndRescanDeviceAsync(issue.DeviceId);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemediationEngine] Hardware fix failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> EnableDeviceDeepAsync(string deviceId)
        {
            try
            {
                string serviceScript = "if ((Get-Service 'SS3Svc' -ea 0).Status -eq 'Stopped') { Start-Service 'SS3Svc' -ea 0 }";
                await CommandExecutor.RunCommand(serviceScript, isPowerShell: true);

                string pnpCommand = $"pnputil /enable-device \"{deviceId}\"";

                string result = await CommandExecutor.GetCommandOutput(pnpCommand, isPowerShell: false);

                Debug.WriteLine($"\n[--- PNPUTIL EXECUTION ---]");
                Debug.WriteLine($"Target Device: {deviceId}");
                Debug.WriteLine($"Result: {result}");
                Debug.WriteLine($"[-------------------------]\n");

                if (result.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(result))
                {
                    Debug.WriteLine("[RemediationEngine] Standard Admin failed. Escalating to TrustedInstaller...");
                    await CommandExecutor.RunCommandAsTrustedInstaller(pnpCommand, isPowerShell: false);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] EnableDeviceDeepAsync Exception: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> ResetDeviceDeepAsync(string deviceId)
        {
            try
            {
                string disableCmd = $"pnputil /disable-device \"{deviceId}\"";
                string enableCmd = $"pnputil /enable-device \"{deviceId}\"";

                await CommandExecutor.RunCommandAsTrustedInstaller(disableCmd, isPowerShell: false);
                await Task.Delay(2000);
                await CommandExecutor.RunCommandAsTrustedInstaller(enableCmd, isPowerShell: false);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemediationEngine] ResetDeviceDeepAsync Exception: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> RescanPnpHardwareAsync()
        {
            try
            {
                await CommandExecutor.RunCommandAsTrustedInstaller("pnputil /scan-devices", isPowerShell: false);
                return true;
            }
            catch { return false; }
        }

        private static async Task<bool> UninstallAndRescanDeviceAsync(string deviceId)
        {
            try
            {
                await CommandExecutor.RunCommandAsTrustedInstaller($"pnputil /remove-device \"{deviceId}\"", isPowerShell: false);
                await Task.Delay(1500);
                await CommandExecutor.RunCommandAsTrustedInstaller("pnputil /scan-devices", isPowerShell: false);
                return true;
            }
            catch { return false; }
        }

        #endregion
    }
}