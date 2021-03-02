using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.UI
{
    [RequireComponent(typeof(Text))]
    public class TextCompositeString : MonoBehaviour
    {
        public Text text { get; private set; }
        [Tooltip("Leave empty to use Text.text as default")]
        public string defaultText;
        // note :: can improve to have support for more languages
        // public List<languageData> languages_defaultText

        void Awake()
        {
            text = GetComponent<Text>();
            if (string.IsNullOrEmpty(defaultText))
                defaultText = text.text;
        }

        /// <summary>
        /// Update the Text with given format item {0}
        /// </summary>
        /// <param name="arg">Argument to replace {0}</param>
        public void UpdateArgument(object arg)
        {
            text.text = string.Format(defaultText, arg);
        }

        /// <summary>
        /// Update the Text with given format items [{0},{1}...]
        /// </summary>
        /// <param name="args">Argument to replace {0}, {1} ...</param>
        public void UpdateArguments(object[] args)
        {
            text.text = string.Format(defaultText, args);
        }

        /// <summary>
        /// Changes language of Text. Update is not visible until arguments are updated.
        /// </summary>
        /// <param name="language">New language</param>
        /// <returns>False if language not found</returns>
        public bool SetLanguge(string language)
        {
            // psudeocode :: defaultText = languges.Find(language);
            throw new System.NotImplementedException();
        }
    }
}