using System.Collections.Generic;

namespace TocflQuiz.Models
{
    public sealed class FeatureContext
    {
        public string FeatureKey { get; }
        public IReadOnlyList<CourseModule> SelectedModules { get; }

        public FeatureContext(string featureKey, IReadOnlyList<CourseModule> selectedModules)
        {
            FeatureKey = featureKey ?? string.Empty;
            SelectedModules = selectedModules ?? new List<CourseModule>();
        }
    }
}
