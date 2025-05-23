﻿using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.Application.Settings;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration.Api;
using JetBrains.ReSharper.Plugins.Unity.Utils;
using JetBrains.ReSharper.Psi;

namespace JetBrains.ReSharper.Plugins.Unity.CSharp.Feature.Services.Refactorings.Rename
{
    [ShellFeaturePart]
    public class FormerlySerializedAsAtomicRenameFactory : IAtomicRenameFactory
    {
        public bool IsApplicable(IDeclaredElement declaredElement)
        {
            if (!declaredElement.IsFromUnityProject())
                return false;

            var unityApi = declaredElement.GetSolution().GetComponent<UnityApi>();
            return 
                unityApi.IsSerialisedField(declaredElement as IField).HasFlag(SerializedFieldStatus.UnitySerializedField)
                || unityApi.IsSerialisedAutoProperty(declaredElement as IProperty, true).HasFlag(SerializedFieldStatus.UnitySerializedField);
        }

        public RenameAvailabilityCheckResult CheckRenameAvailability(IDeclaredElement element)
        {
            return RenameAvailabilityCheckResult.CanBeRenamed;
        }

        public IEnumerable<AtomicRenameBase> CreateAtomicRenames(IDeclaredElement declaredElement, string newName,
            bool doNotAddBindingConflicts)
        {
            var settingsStore = declaredElement.GetSolution().GetComponent<ISettingsStore>();
            var knownTypesCache = declaredElement.GetSolution().GetComponent<KnownTypesCache>();
            return [new FormerlySerializedAsAtomicRename(declaredElement, newName, settingsStore, knownTypesCache)];
        }
    }
}