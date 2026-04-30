using UnityEngine;
using UnityEditor;


[CustomPropertyDrawer(typeof(HazardManager.Hazard))]
public class HazardDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Récupère les sous-propriétés pour construire le label
        SerializedProperty targetProp = property.FindPropertyRelative("target");
        SerializedProperty typeProp = property.FindPropertyRelative("movementType");

        // Construit un label personnalisé
        string customLabel;

        if (targetProp != null && targetProp.objectReferenceValue != null)
        {
            string targetName = targetProp.objectReferenceValue.name;
            string typeName = typeProp != null
                ? ((HazardManager.MovementType)typeProp.enumValueIndex).ToString()
                : "?";
            customLabel = $"{targetName}  [{typeName}]";
        }
        else
        {
            // Si pas de target assigné, garde le nom par défaut
            customLabel = $"{label.text}  (vide)";
        }

        // Dessine le foldout avec le label personnalisé
        EditorGUI.PropertyField(position, property, new GUIContent(customLabel), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Retourne la hauteur par défaut (gère foldout ouvert / fermé)
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}