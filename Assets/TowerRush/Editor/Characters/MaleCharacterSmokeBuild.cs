#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HeatRise.EditorTools
{
    public static class MaleCharacterSmokeBuild
    {
        public static void Build()
        {
            MaleCharacterIntegrationChecks.ValidateInstallation();
            HeatRise.Editor.HeatRiseValidation.ValidateScenes();
            string output = Path.GetFullPath("Builds/CharacterIntegration/HeatRise.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MaleCharacterIntegrationChecks.Lan, MaleCharacterIntegrationChecks.Solo },
                locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Character integration build failed: " + report.summary.totalErrors);
            Debug.Log("CHARACTER_BUILD PASS: " + output);
        }
    }
}
#endif
