﻿using UnityEngine;
using UnityEngine.UI;

namespace ScrollableLobbyUI
{
    public class ScrollButtonsController : MonoBehaviour
    {
        public GameObject left;
        public GameObject right;
        public float deadzone = 0.0001F;

        private ScrollRect scrollRect;
        private RectTransform rectTransform;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            var contentOutOfRect = rectTransform.rect.width < scrollRect.content.rect.width;

            left.SetActive(contentOutOfRect && scrollRect.horizontalNormalizedPosition > deadzone);
            right.SetActive(contentOutOfRect && scrollRect.horizontalNormalizedPosition < 1 - deadzone);

        }
    }
}
