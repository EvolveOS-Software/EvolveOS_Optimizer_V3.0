// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public record SystemHealthResult(string ImagePath, string StatusText, int PenaltyScore);

    public static class SystemHealthHelper
    {
        public static SystemHealthResult EvaluateHealth(
            double ramPercentage, double totalRamGb,
            double vRamPercentage, double totalVRamGb,
            double junkGigabytes)
        {
            int penaltyScore = 0;

            double freeRamGb = totalRamGb - (totalRamGb * (ramPercentage / 100.0));
            if (freeRamGb <= 1.5 || ramPercentage >= 95.0) penaltyScore += 2;
            else if (freeRamGb <= 3.5 || ramPercentage >= 85.0) penaltyScore += 1;

            double usedVRamGb = totalVRamGb * (vRamPercentage / 100.0);
            if (usedVRamGb >= 20.0 || vRamPercentage >= 95.0) penaltyScore += 2;
            else if (usedVRamGb >= 12.0 || vRamPercentage >= 85.0) penaltyScore += 1;

            double driveTotalGb = 256.0;
            try
            {
                System.IO.DriveInfo cDrive = new System.IO.DriveInfo("C");
                if (cDrive.IsReady)
                {
                    driveTotalGb = cDrive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Failed to read C: drive info: {ex.Message}");
            }

            double junkPercentage = (junkGigabytes / driveTotalGb) * 100;
            if (junkPercentage >= 15.0 || junkGigabytes >= 35.0) penaltyScore += 2;
            else if (junkPercentage >= 10.0 || junkGigabytes >= 20.0) penaltyScore += 1;

            string imagePath;
            string statusText;

            if (penaltyScore >= 4)
            {
                imagePath = "ms-appx:///Assets/PngImages/health_critical.png";
                statusText = ResourceString.GetString("Health_Poor") ?? "Poor - Action Required";
            }
            else if (penaltyScore >= 2)
            {
                imagePath = "ms-appx:///Assets/PngImages/health_warning.png";
                statusText = ResourceString.GetString("Health_Warning") ?? "Fair - Optimization Recommended";
            }
            else
            {
                imagePath = "ms-appx:///Assets/PngImages/health_good.png";
                statusText = ResourceString.GetString("Health_Good") ?? "Good - System is Healthy";
            }

            return new SystemHealthResult(imagePath, statusText, penaltyScore);
        }
    }
}
