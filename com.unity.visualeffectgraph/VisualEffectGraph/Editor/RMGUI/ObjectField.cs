using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine.Experimental.UIElements.StyleEnums;
using System.Collections.Generic;


namespace UnityEditor.VFX.UIElements
{
    class ObjectField : ValueControl<Object>
    {
        VisualElement m_IconContainer;
        VisualElement m_NameContainer;
        VisualElement m_SelectContainer;

        class Receiver : ObjectSelectorReceiver
        {
            public ObjectField m_ObjectField;


            public override void OnSelectionChanged(Object selection)
            {
                m_ObjectField.ValueChanged(selection);
            }

            public override void OnSelectionClosed(Object selection)
            {
                ObjectSelector.get.objectSelectorReceiver = null;
            }
        }


        Receiver m_Reciever;


        public System.Type editedType { get; set; }


        void OnShowObjects()
        {
            ObjectSelector.get.Show(GetValue(), editedType, null, false);
            ObjectSelector.get.objectSelectorReceiver = m_Reciever;
        }

        void OnSelect()
        {
            panel.focusController.SwitchFocus(this);
        }

        public ObjectField(string label) : base(label)
        {
            Setup();
        }

        public ObjectField(VisualElement existingLabel) : base(existingLabel)
        {
            Setup();
        }

        void ValueChanged(Object value)
        {
            SetValue(value);
            if (OnValueChanged != null)
            {
                OnValueChanged();
            }
        }

        void Setup()
        {
            m_NameContainer = new VisualElement();
            m_NameContainer.style.flex = 1;

            m_IconContainer = new VisualElement();
            m_IconContainer.style.width = 13;


            m_SelectContainer = new VisualElement();
            //style.backgroundImage = EditorStyles.objectField.normal.background;
            style.sliceLeft = EditorStyles.objectField.border.left;
            style.sliceRight = EditorStyles.objectField.border.right;
            style.sliceTop = EditorStyles.objectField.border.bottom;
            style.sliceBottom = EditorStyles.objectField.border.top;

            m_SelectContainer.style.width = EditorStyles.objectField.border.right;

            Add(m_IconContainer);
            Add(m_NameContainer);
            Add(m_SelectContainer);

            m_SelectContainer.AddManipulator(new Clickable(OnShowObjects));
            this.AddManipulator(new Clickable(OnSelect));
            this.AddManipulator(new ShortcutHandler(new Dictionary<Event, ShortcutDelegate>
            {
                { Event.KeyboardEvent("delete"), SetToNull }
            }));

            m_Reciever = Receiver.CreateInstance<Receiver>();
            m_Reciever.m_ObjectField = this;

            style.flexDirection = FlexDirection.Row;
            focusIndex = 0;
        }

        EventPropagation SetToNull()
        {
            ValueChanged(null);

            return EventPropagation.Stop;
        }

        protected override void ValueToGUI()
        {
            Object value = GetValue();
            var temp = EditorGUIUtility.ObjectContent(value, editedType);

            m_IconContainer.style.backgroundImage = temp.image as Texture2D;

            m_IconContainer.style.width = m_IconContainer.style.backgroundImage.value == null ? 0 : 18;
            m_NameContainer.text = value == null ? "null" : value.name;
        }
    }
}
