﻿using MinerPlugin;
using MinerPluginToolkitV1;
using MinerPluginToolkitV1.Configs;
using MinerPluginToolkitV1.ExtraLaunchParameters;
using MinerPluginToolkitV1.Interfaces;
using NiceHashMinerLegacy.Common;
using NiceHashMinerLegacy.Common.Algorithm;
using NiceHashMinerLegacy.Common.Device;
using NiceHashMinerLegacy.Common.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZEnemy
{
    class ZEnemyPlugin : IMinerPlugin, IInitInternals, IBinaryPackageMissingFilesChecker
    {
        public Version Version => new Version(1, 1);

        public string Name => "ZEnemy";

        public string Author => "Domen Kirn Krefl";

        public string PluginUUID => "881c5230-4bfc-11e9-a481-e144ccd86993";

        public Dictionary<BaseDevice, IReadOnlyList<Algorithm>> GetSupportedAlgorithms(IEnumerable<BaseDevice> devices)
        {
            var cudaGpus = devices.Where(dev => dev is CUDADevice cuda && cuda.SM_major >= 6).Cast<CUDADevice>();
            var supported = new Dictionary<BaseDevice, IReadOnlyList<Algorithm>>();
            var minDrivers = new Version(411, 0);
            if (CUDADevice.INSTALLED_NVIDIA_DRIVERS < minDrivers) return supported;

            foreach (var gpu in cudaGpus)
            {
                var algos = GetSupportedAlgorithms(gpu).ToList();
                if (algos.Count > 0) supported.Add(gpu, algos);
            }

            return supported;
        }

        private IEnumerable<Algorithm> GetSupportedAlgorithms(CUDADevice dev)
        {
            yield return new Algorithm(PluginUUID, AlgorithmType.X16R);
            yield return new Algorithm(PluginUUID, AlgorithmType.Skunk);
        }

        public IMiner CreateMiner()
        {
            return new ZEnemy(PluginUUID)
            {
                MinerOptionsPackage = _minerOptionsPackage,
                MinerSystemEnvironmentVariables = _minerSystemEnvironmentVariables
            };
        }

        public bool CanGroup(MiningPair a, MiningPair b)
        {
            return a.Algorithm.FirstAlgorithmType == b.Algorithm.FirstAlgorithmType;
        }

        #region Internal Settings
        public void InitInternals()
        {
            var pluginRoot = Path.Combine(Paths.MinerPluginsPath(), PluginUUID);

            var readFromFileEnvSysVars = InternalConfigs.InitMinerSystemEnvironmentVariablesSettings(pluginRoot, _minerSystemEnvironmentVariables);
            if (readFromFileEnvSysVars != null) _minerSystemEnvironmentVariables = readFromFileEnvSysVars;

            var fileMinerOptionsPackage = InternalConfigs.InitInternalsHelper(pluginRoot, _minerOptionsPackage);
            if (fileMinerOptionsPackage != null) _minerOptionsPackage = fileMinerOptionsPackage;
        }

        private static MinerOptionsPackage _minerOptionsPackage = new MinerOptionsPackage
        {
            GeneralOptions = new List<MinerOption>
            {
                /// <summary>
                /// GPU intensity 8.0-31.0, decimals allowed (default: 19)
                /// </summary>
                new MinerOption
                {
                    Type = MinerOptionType.OptionWithMultipleParameters,
                    ID = "zenemy_intensity",
                    ShortName = "-i",
                    LongName = "--intensity=",
                    DefaultValue = "19",
                    Delimiter = ","
                },
                /// <summary>
                /// set CUDA scheduling option:
                /// 0: BlockingSync (default)
                /// 1: Spin
                /// 2: Yield
                /// </summary>
                new MinerOption
                {
                    Type = MinerOptionType.OptionWithSingleParameter,
                    ID = "zenemy_cudaSchedula",
                    LongName = "--cuda-schedule",
                    DefaultValue = "0",
                },
                /// <summary>
                /// set process priority (default: 3) 0 idle, 2 normal to 5 highest
                /// </summary>
                new MinerOption
                {
                    Type = MinerOptionType.OptionWithSingleParameter,
                    ID = "zenemy_priority",
                    ShortName = "--cpu-priority",
                    DefaultValue = "3"
                },
                //TODO WARNING this functionality can overlap with already implemented one!!!
                /// <summary>
                /// set process affinity to cpu core(s), mask 0x3 for cores 0 and 1
                /// </summary>
                new MinerOption
                {
                    Type = MinerOptionType.OptionWithSingleParameter,
                    ID = "zenemy_affinity",
                    ShortName = "--cpu-affinity",
                }
            },
            TemperatureOptions = new List<MinerOption>
            {
                /// <summary>
                /// Only mine if gpu temperature is less than specified value
                /// Can be tuned with --resume-temp=N to set a resume value
                /// </summary>
                new MinerOption
                {
                    Type = MinerOptionType.OptionWithSingleParameter,
                    ID = "zenemy_maxTemperature",
                    ShortName = "--max-temp=",
                },
                /// <summary>
                /// resume value for miners to start again after shutdown
                /// </summary>
                new MinerOption
                {
                    Type = MinerOptionType.OptionWithSingleParameter,
                    ID = "zenemy_resumeTemperature",
                    ShortName = "--resume-temp=",
                }
            }
        };

        protected static MinerSystemEnvironmentVariables _minerSystemEnvironmentVariables = new MinerSystemEnvironmentVariables { };
        #endregion Internal Settings

        public IEnumerable<string> CheckBinaryPackageMissingFiles()
        {
            var miner = CreateMiner() as IBinAndCwdPathsGettter;
            if (miner == null) return Enumerable.Empty<string>();
            var pluginRootBinsPath = miner.GetBinAndCwdPaths().Item2;
            return BinaryPackageMissingFilesCheckerHelpers.ReturnMissingFiles(pluginRootBinsPath, new List<string> { "vcruntime140.dll", "z-enemy.exe" });
        }
    }
}