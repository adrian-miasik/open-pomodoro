using System;
using AdrianMiasik.Components.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdrianMiasik.Components
{
    public class DoubleDigit : MonoBehaviour, ISelectHandler, IPointerClickHandler, ISubmitHandler
    {
        [Header("References")] 
        [SerializeField] private Selectable selectable;
        [SerializeField] private Image background;

        [SerializeField] private ClickButton upArrow;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private ClickButton downArrow;

        [Header("Color")] 
        [SerializeField] private float animationDuration = 0.25f;
        [SerializeField] private AnimationCurve animationRamp = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Wobble")] 
        [SerializeField] private Animation pulseWobble;

        private PomodoroTimer.Digits digit;
        private PomodoroTimer timer;
        private bool isInteractable;
        private bool isSelected;

        // Color animation
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Color selectionColor;
        private Color startingColor;
        private Color endingColor;
        private bool isColorAnimating;
        private float accumulatedColorTime;
        private float progress;

        // Deselection
        private float accumulatedSelectionTime;
        private float deselectionDuration = 3f; // How long to wait before deselecting automatically on touch devices

        // Controls
        private TMP_SelectionCaret caret;
        private bool ignoreFirstClick = true;

        // Shaders
        private Material _instanceMaterial;
        private static readonly int SquircleColor = Shader.PropertyToID("Color_297012532bf444df807f8743bdb7e4fd");

        // Unity Events
        public UnityEvent OnSelection;

        public void Initialize(PomodoroTimer.Digits digit, PomodoroTimer timer, int digits)
        {
            this.digit = digit;
            this.timer = timer;

            // Disable run time caret interactions - We want to run input through this classes input events
            caret = input.textViewport.GetChild(0).GetComponent<TMP_SelectionCaret>();
            if (caret)
            {
                // Prevent input field from getting selection focus
                caret.raycastTarget = false;
            }

            SetSquircleColor(color);
            SetDigitsLabel(digits);
            HideArrows();
            pulseWobble.Stop();
        }

        private void Update()
        {
            if (isColorAnimating)
            {
                accumulatedColorTime += Time.deltaTime;
                progress = accumulatedColorTime / animationDuration;
                if (progress >= 1)
                {
                    isColorAnimating = false;
                }

                float evaluatedTime = animationRamp.Evaluate(progress);

                // Animate and modify
                _instanceMaterial.SetColor(SquircleColor, Color.Lerp(startingColor, endingColor, evaluatedTime));

                // Apply
                background.material = _instanceMaterial;
            }

            if (isSelected)
            {
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    if (!input.isFocused)
                    {
                        SetValue(digit, "0");
                    }
                }
                
                // Scroll input
                if (Input.mouseScrollDelta.y > 0)
                {
                    if (timer.CanIncrementOne(digit))
                    {
                        upArrow.OnPointerClick(null);
                    }
                }
                else if (Input.mouseScrollDelta.y < 0)
                {
                    if (timer.CanDecrementOne(digit))
                    {
                        downArrow.OnPointerClick(null);
                    }
                }

                // Arrow keys : Up arrow
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    upArrow.OnPointerClick(null);
                    upArrow.Hold();
                }
                else if (Input.GetKeyUp(KeyCode.UpArrow))
                {
                    upArrow.Release();
                }

                // Arrow keys : Down arrow
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    downArrow.OnPointerClick(null);
                    downArrow.Hold();
                }
                else if (Input.GetKeyUp(KeyCode.DownArrow))
                {
                    downArrow.Release();
                }

                // Automatic digit deselection on mobile
                if (Input.touchSupported && !input.isFocused)
                {
                    accumulatedSelectionTime += Time.deltaTime;

                    if (accumulatedSelectionTime > deselectionDuration)
                    {
                        accumulatedSelectionTime = 0;
                        DeselectInput();
                        timer.ClearSelection();
                    }
                }
            }
        }

        private void SetSquircleColor(Color color)
        {
            // Create instance material
            if (_instanceMaterial == null)
            {
                _instanceMaterial = new Material(background.material);
            }

            startingColor = _instanceMaterial.GetColor(SquircleColor);

            endingColor = color;
            accumulatedColorTime = 0;
            isColorAnimating = true;
        }

        public void SetDigitsLabel(int digits)
        {
            // If this digit value is actually different...
            if (digits.ToString("D2") != input.text)
            {
                // Update the digit
                input.text = digits.ToString("D2");
            }
        }

        public string GetDigitsLabel()
        {
            return input.text;
        }

        public Selectable GetSelectable()
        {
            return selectable;
        }
        
        private void HideArrows()
        {
            upArrow.Hide();
            downArrow.Hide();
        }

        private void ShowArrows()
        {
            if (timer != null)
            {
                if (timer.CanIncrementOne(digit))
                {
                    upArrow.Show();
                }

                if (timer.CanDecrementOne(digit))
                {
                    downArrow.Show();        
                }
            }
        }

        public void Lock()
        {
            isInteractable = false;
            input.interactable = false;
        }

        public void Unlock()
        {
            isInteractable = true;
            input.interactable = true;
        }
        
        public void SetTextColor(Color newColor)
        {
            input.textComponent.color = newColor;
        }
        
        // Unity Events
        public void SetValue(PomodoroTimer.Digits digit, string value)
        {
            switch (digit)
            {
                case PomodoroTimer.Digits.HOURS:
                    SetHours(value);
                    break;
                case PomodoroTimer.Digits.MINUTES:
                    SetMinutes(value);
                    break;
                case PomodoroTimer.Digits.SECONDS:
                    SetSeconds(value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(digit), digit, null);
            }
        }
        
        public void SetHours(string hours)
        {
            if (timer != null)
            {
                timer.SetHours(hours);
                UpdateArrows();
            }
        }

        public void SetMinutes(string minutes)
        {
            if (timer != null)
            {
                timer.SetMinutes(minutes);
                UpdateArrows();
            }
        }

        public void SetSeconds(string seconds)
        {
            if (timer != null)
            {
                timer.SetSeconds(seconds);
                UpdateArrows();
            }
        }

        public void IncrementOne()
        {
            if (timer != null)
            {
                timer.IncrementOne(digit);
                SetDigitsLabel(timer.GetDigitValue(digit));
                pulseWobble.Stop();
                pulseWobble.Play();
                UpdateArrows();
                accumulatedSelectionTime = 0;
            }
        }

        public void DecrementOne()
        {
            if (timer != null)
            {
                timer.DecrementOne(digit);
                SetDigitsLabel(timer.GetDigitValue(digit));
                pulseWobble.Stop();
                pulseWobble.Play();
                UpdateArrows();
                accumulatedSelectionTime = 0;
            }
        }

        private void UpdateArrows()
        {
            // Up Arrow
            if (timer.CanIncrementOne(digit))
            {
                upArrow.Show();
            }
            else
            {
                upArrow.Hide();
            }
            
            // Down Arrow
            if (timer.CanDecrementOne(digit))
            {
                downArrow.Show();
            }
            else
            {
                downArrow.Hide();
            }
        }

        public void Highlight()
        {
            OnSelect(null, false);
        }
        
        public void OnSelect(BaseEventData eventData)
        {
            OnSelect(eventData, true);
        }
        
        public void OnSelect(BaseEventData eventData, bool setSelection)
        {
            if (!isInteractable)
            {
                return;
            }

            isSelected = true;
            accumulatedSelectionTime = 0;
            
            ShowArrows();
            SetSquircleColor(selectionColor);

            if (setSelection)
            {
                timer.SetSelection(this);
            }

            OnSelection.Invoke();
        }

        public void Deselect()
        {
            isSelected = false;
            ignoreFirstClick = true;
            
            HideArrows();
            SetSquircleColor(color);
            
            // Disable caret selection
            caret.raycastTarget = false;
            input.DeactivateInputField();
        }

        public void DeselectInput()
        {
            // Disable caret selection
            caret.raycastTarget = false;
            input.DeactivateInputField();

            accumulatedSelectionTime = 0;
            
            // Select self (Deselect input field)
            selectable.Select();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // First click is reserved for digit selection, second click is for editing the input field
            if (ignoreFirstClick)
            {
                ignoreFirstClick = false;
                return;
            }

            if (isSelected)
            {
                caret.raycastTarget = true;
                input.ActivateInputField();
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!caret.raycastTarget)
            {
                caret.raycastTarget = true;
                input.ActivateInputField();
            }
            else
            {
                caret.raycastTarget = false;
                input.DeactivateInputField(true);
            }
        }
    }
}