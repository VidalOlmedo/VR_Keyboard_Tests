﻿//////////
//  To render a laser pointer in the controller to interact with UI elements (add colliders to UI elements, can be as triggers)
//  Script attached to: LaserPointer (empty GameObject, must be attached to one of the hands).
//
//  If using the Keyboard, drag the keyboard prefab into the scene to create an instance and assign that instance to the keyboard field in this component
//
//  Note: do not change the Direction field of a slider, rotate it instead
//////////
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Valve.VR;
using UnityEngine.UI;

public class LaserPointer : MonoBehaviour
{
    [Tooltip("The hand to attach the laser pointer to")]
    public GameObject hand;
    [Tooltip("The keyboard instance in the scene")]
    public GameObject keyboard;
    public Material colorPickerTarget;
    public SteamVR_Action_Boolean trigger;
    public float laserDistance = 1;
    [Tooltip("Define and add this tags to input fields and sliders/scrollbars - UI Layer is the default for all UI elements")]
    public string UI_Layer = "UI", InputField_Tag = "InputField", SliderScroll_Tag = "Slider_Scroll", ColorPicker_Tag = "Colors";

    private GameObject currentUI = null;
    private List<GameObject> previousUI = new List<GameObject>();
    private LineRenderer laser;
    private GameObject dot;
    private Slider slider;
    private Scrollbar scrollbar;
    private Texture2D colorPicker;
    private Vector2 colorPickerSize;
    private SteamVR_Input_Sources _hand;
    private bool hovering;
    private bool firstTime = true;


    //================================================================================
    private void Start()
    {
        transform.parent = hand.transform;
        laser = GetComponent<LineRenderer>();
        dot = transform.GetChild(0).gameObject;

        //Get the hand that the LaserPointer game object is attached to
        if (hand.name == "RightHand")
            _hand = SteamVR_Input_Sources.RightHand;
        else if (hand.name == "LeftHand")
            _hand = SteamVR_Input_Sources.LeftHand;
    }



    //================================================================================
    private void Update()
    {
        RaycastHit hit;
        PointerEventData pointer = new PointerEventData(EventSystem.current);

        //Creates a ray from the position of the hand pointing forward
        Ray ray = new Ray(transform.parent.position, transform.parent.forward);

        //Casts the ray to detect UI elements
        if (Physics.Raycast(ray, out hit, laserDistance, 1 << LayerMask.NameToLayer(UI_Layer))) //https://answers.unity.com/questions/1164722/raycast-ignore-layers-except.html
        {
            hovering = true;

            //Gets the UI element hit and adds it to the list of previous hits
            currentUI = hit.collider.gameObject;
            CheckPreviousUI(currentUI);

            //Executes the hover state of the UI element hit
            ExecuteEvents.Execute(currentUI, pointer, ExecuteEvents.pointerEnterHandler);

            //If the trigger is pressed while pointing the UI element and is not a slider, clicks it
            if (trigger.GetStateDown(_hand) && currentUI.CompareTag(SliderScroll_Tag) == false)
            {
                //If the clicked UI element is an input field, opens the keyboard
                if (currentUI.CompareTag(InputField_Tag) && keyboard != null)
                    OpenKeyboard(currentUI.GetComponent<InputField>());

                //Clicks the UI element
                StartCoroutine(PressButton(pointer));
            }

            //If the UI element is a slider, moves it while pulling the trigger;
            else if (trigger.GetState(_hand) && currentUI.CompareTag(SliderScroll_Tag))
            {
                //Takes the scrollbar or slider reference
                if (firstTime)
                {
                    if (currentUI.GetComponent<Slider>() != null)
                    {
                        slider = currentUI.GetComponent<Slider>();
                        scrollbar = null;
                    }
                    else if (currentUI.GetComponent<Scrollbar>() != null)
                    {
                        scrollbar = currentUI.GetComponent<Scrollbar>();
                        slider = null;
                    }

                    firstTime = false;
                }

                //Moves the slider/scrollbar
                MoveSlider(currentUI, hit);
            }
            else if (trigger.GetState(_hand) && currentUI.CompareTag(ColorPicker_Tag))
            {
                //Get the texture of the colors sprite and the size of its collider
                if (firstTime)
                {
                    colorPicker = currentUI.GetComponent<Image>().sprite.texture;
                    colorPickerSize = currentUI.GetComponent<BoxCollider>().size;
                    
                    firstTime = false;
                }

                //Picks the color
                PickColor(currentUI, hit);
            }
            else
                firstTime = true;
        }

        //If no UI element is being pointed
        else if (hovering)
        {
            //Executes the 'unhovered' state (deselect) of the previous elements hit (to prevent elements to remain 'hovered' while moving the laser quickly over elements)
            foreach(GameObject previous in previousUI)
            {
                ExecuteEvents.Execute(previous, pointer, ExecuteEvents.pointerExitHandler);
            }
                
            hovering = false;
            firstTime = true;
        }

        //Set the length of the laser and the position of the dot
        SetLaser(hit);

        //Debug.DrawRay(transform.parent.position, transform.parent.forward * laserLength);
    }



    //================================================================================
    //Set the length of the laser and the position of the dot
    private void SetLaser(RaycastHit hit)
    {
        Vector3[] laserPoints = new Vector3[2];
        //Points to draw the line:
        //laserPoints[0] -> origin
        //laserPoints[1] -> end point

        //Sets the origin point of the line rendered to the hand's position
        laserPoints[0] = transform.parent.position;

        //If the laser is pointing an UI element, sets the end point of the line to it
        if (hit.collider != null)
            laserPoints[1] = hit.point;
        //Else, sets the default length of th line
        else
            laserPoints[1] = transform.parent.position + (transform.parent.forward * laserDistance);

        //Sets the position of the line
        laser.SetPositions(laserPoints);
        //Set the position of the dot to the end point
        dot.transform.position = laserPoints[1];
    }



    //================================================================================
    //Executes the click event on the UI element
    private IEnumerator PressButton(PointerEventData pointer)
    {
        ExecuteEvents.Execute(currentUI, pointer, ExecuteEvents.pointerClickHandler);   //Click the element
        ExecuteEvents.Execute(currentUI, pointer, ExecuteEvents.pointerDownHandler);    //Executes the pressed state of the element
        yield return new WaitForSeconds(0.1f);  //Wait to make the effect visible
        ExecuteEvents.Execute(currentUI, pointer, ExecuteEvents.pointerUpHandler);  //Executes the released state of the element
        ExecuteEvents.Execute(currentUI, pointer, ExecuteEvents.deselectHandler);   //Makes the UI element to get back its unhovered style
    }



    //================================================================================
    //Updates the list of previous UI elements clicked to deselect them later
    private void CheckPreviousUI(GameObject currentUI)
    {
        bool found = false;

        //If the current UI element is already in the list, is not added to the list
        foreach(GameObject previous in previousUI)
        {
            if(currentUI == previous)
            {
                found = true;
                break;
            }
        }

        //Else, adds it to the list (30 elements max)
        if(found == false)
        {
            previousUI.Insert(0, currentUI);

            if (previousUI.Count > 30)
                previousUI.RemoveAt(30);
        }
    }



    //================================================================================
    //Gets the color from the colors sprite and assign it to the target
    private void PickColor(GameObject currentUI, RaycastHit hit)
    {
        //Gets the hit point in color picker's local space
        Vector2 hitPoint = currentUI.transform.InverseTransformPoint(hit.point);

        //Converts the raw coordinates(positive or negative) into a 0 % to 100 % value of the colorPickerSize
        hitPoint.x = (colorPickerSize.x / 2) + hitPoint.x;
        hitPoint.y = (colorPickerSize.y / 2) + hitPoint.y;

        //Get the current pixel's color according to the hit point and assign it to the target
        colorPickerTarget.color = colorPicker.GetPixel((int)hitPoint.x, (int)hitPoint.y);
    }



    //================================================================================
    //Moves the slider/scrollbar
    private void MoveSlider(GameObject currentUI, RaycastHit hit)
    {
        //Slider's collider's width or height
        float sliderSize = 0;
        //Hit point in slider's local space (-halfSliderSize --> 0 --> halfSliderSize)
        float hitPoint = 0;


        //If the current UI is a scrollbar
        if (scrollbar != null)
        {
            //If it is a vertical scrollbar, takes the height and the vertical hitpoint
            if(scrollbar.direction == Scrollbar.Direction.BottomToTop || scrollbar.direction == Scrollbar.Direction.TopToBottom)
            {
                sliderSize = currentUI.GetComponent<BoxCollider>().size.y;
                hitPoint = currentUI.transform.InverseTransformPoint(hit.point).y;
                hitPoint = sliderSize + hitPoint;   //Coordinates in vertical scroll are negative, this converts and inverts the hit point
            }
            //If it is a horizontal scrollbar or a slider, takes the width and the horizontal hitpoint
            else
            {
                sliderSize = currentUI.GetComponent<BoxCollider>().size.x;
                hitPoint = currentUI.transform.InverseTransformPoint(hit.point).x;
            }

            //If the scrollbar is not part of a ScrollView, converts the raw coordinates (positive or negative) into a 0% to 100% value of the sliderSize
            if (scrollbar.transform.parent.GetComponent<ScrollRect>() == null)
            {
                hitPoint = (sliderSize / 2) + hitPoint;
            }
        }
        //If it is a slider
        else
        {
            sliderSize = currentUI.GetComponent<BoxCollider>().size.x;
            hitPoint = currentUI.transform.InverseTransformPoint(hit.point).x;

            hitPoint = (sliderSize / 2) + hitPoint;
        }


        //Set the value of the slider/scrollbar (0 - 1)
        if(slider != null)
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, hitPoint / sliderSize);

        else if(scrollbar != null)
            scrollbar.value = hitPoint / sliderSize;
    }



    //================================================================================
    //Enables the keyboard an sets its target input field to the one selected
    private void OpenKeyboard(InputField inputField)
    {
        keyboard.SetActive(true);
        keyboard.GetComponent<Keyboard>().output = inputField;
    }
}