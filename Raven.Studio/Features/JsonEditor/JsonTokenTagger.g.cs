namespace Raven.Studio.Features.JsonEditor {
    using ActiproSoftware.Text;
    using ActiproSoftware.Text.Tagging.Implementation;
    using System;
    
    
    /// <summary>
    /// Represents a token tagger for the <c>Json</c> language.
    /// </summary>
    /// <remarks>
    /// This type was generated by the Actipro Language Designer tool v12.1.561.0 (http://www.actiprosoftware.com).
    /// </remarks>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("LanguageDesigner", "12.1.561.0")]
    public partial class JsonTokenTagger : TokenTagger {
        
        private IJsonClassificationTypeProvider classificationTypeProviderValue;
        
        /// <summary>
        /// Initializes a new instance of the <c>JsonTokenTagger</c> class.
        /// </summary>
        /// <param name="document">The specific <see cref="ICodeDocument"/> for which this token tagger will be used.</param>
        /// <param name="classificationTypeProvider">A <see cref="IJsonClassificationTypeProvider"/> that provides classification types used by this token tagger.</param>
        public JsonTokenTagger(ICodeDocument document, IJsonClassificationTypeProvider classificationTypeProvider) : 
                base(document) {
            if ((classificationTypeProvider == null)) {
                throw new ArgumentNullException("classificationTypeProvider");
            }

            // Initialize
            this.classificationTypeProviderValue = classificationTypeProvider;
        }
        
        /// <summary>
        /// Gets the <see cref="IJsonClassificationTypeProvider"/> that provides classification types used by this token tagger.
        /// </summary>
        /// <value>The <see cref="IJsonClassificationTypeProvider"/> that provides classification types used by this token tagger.</value>
        public IJsonClassificationTypeProvider ClassificationTypeProvider {
            get {
                return this.classificationTypeProviderValue;
            }
        }
    }
}