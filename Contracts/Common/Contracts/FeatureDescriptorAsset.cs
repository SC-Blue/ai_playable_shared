using System;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [CreateAssetMenu(fileName = "feature_descriptor", menuName = "AIPS/Feature Descriptor")]
    public sealed class FeatureDescriptorAsset : ScriptableObject
    {
        [SerializeField] private FeatureDescriptor _descriptor = new FeatureDescriptor();
        [SerializeField, HideInInspector] private bool _contentStoreLocked;
        [SerializeField, HideInInspector] private string _lastValidationError = string.Empty;

        public bool IsRuntimePackage => ToDescriptor().isRuntimePackage;
        public bool IsContentStoreLocked => _contentStoreLocked;
        public string LastValidationError => _lastValidationError ?? string.Empty;

        public FeatureDescriptor ToDescriptor()
        {
            return FeatureDescriptorUtility.Clone(_descriptor);
        }

        public void SetDescriptorForImport(FeatureDescriptor descriptor)
        {
            _descriptor = FeatureDescriptorUtility.Clone(descriptor);
            _contentStoreLocked = true;
            TryValidate(out _lastValidationError);
        }

        public void SetContentStoreLocked(bool locked)
        {
            _contentStoreLocked = locked;
        }

        public bool TryValidate(out string error)
        {
            bool isValid = FeatureDescriptorValidator.TryValidate(
                new[] { ToDescriptor() },
                out error);
            _lastValidationError = error ?? string.Empty;
            return isValid;
        }

        private void OnValidate()
        {
            try
            {
                TryValidate(out _);
            }
            catch (Exception exception)
            {
                _lastValidationError = exception.Message;
            }
        }
    }
}
