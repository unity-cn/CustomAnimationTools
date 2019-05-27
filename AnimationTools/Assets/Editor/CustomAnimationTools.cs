using UnityEngine;
using UnityEditor;
using System.IO;

namespace AnimationTools
{
    public class CustomAnimationTools
    {
        private static int CurveCounter = 0;
        static System.DateTime StartTime;

        [MenuItem("AnimationTool/ProcessRotationInterpolationForConstant %e")]
        static void Execute()
        {
            CurveCounter = 0;
            StartTime = System.DateTime.Now;

            UnityEngine.Object[] selectedObjects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.DeepAssets);

            foreach (UnityEngine.Object selectedObj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(selectedObj);
                if (selectedObj is AnimationClip)
                {
                    Debug.Log("is AnimationClip");
                    ProcessCurveForConstant(path);
                    CurveCounter++;
                }
                else
                {
                    if(path.ToLower().EndsWith(".fbx", System.StringComparison.Ordinal))
                    {
                        Object[] fbxObjects = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var subObj in fbxObjects)
                        {
                            if(subObj is AnimationClip
                                && !subObj.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                            {
                                Debug.Log("Copy animation clip : " + path + " clip name: " + (subObj as AnimationClip).name );
                                string newClipPath = CopyAnimation(subObj as AnimationClip);
                                ProcessCurveForConstant(newClipPath);
                                CurveCounter++;
                            }
                        }
                    }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Time: " + ((System.DateTime.Now - StartTime).TotalMilliseconds / 1000) + "s.");
        }

        static string CopyAnimation(AnimationClip sourceClip)
        {
            string path = AssetDatabase.GetAssetPath(sourceClip);
            path = Path.Combine(Path.GetDirectoryName(path), sourceClip.name) + ".anim";
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            AnimationClip newClip = new AnimationClip();
            EditorUtility.CopySerialized(sourceClip, newClip);
            AssetDatabase.CreateAsset(newClip, newPath);
            //AssetDatabase.Refresh();
            Debug.Log("CopyAnimation: " + newPath);
            return newPath;
        }

        static void ProcessCurveForConstant(string clipPath)
        {
            Debug.Log("Processing : " + clipPath);
            ProcessCurveForConstant(AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath));
        }

        static void ProcessCurveForConstant(AnimationClip animationClip)
        {
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(animationClip);
            EditorUtility.SetDirty(animationClip);

            var soClip = new SerializedObject(animationClip);
            float sampleRate = soClip.FindProperty("m_SampleRate").floatValue;
            float oneKeyframeTime = (float)((int)((1.0f / sampleRate) * 1000)) / 1000 + 0.001f;

            string[] editorCurveSetNames = new string[] { "m_EditorCurves", "m_EulerEditorCurves" };

            foreach (var editorCurveSetName in editorCurveSetNames)
            {
                var curCurveSet = soClip.FindProperty(editorCurveSetName);
                int curCurveSetLenght = curCurveSet.arraySize;
                if (curCurveSetLenght == 0)
                {
                    Debug.Log("Can not fine editor curves in " + editorCurveSetName);
                    continue;
                }

                for (int curveSetIndex = 0; curveSetIndex < curCurveSetLenght; curveSetIndex++)
                {
                    var curCurveInfo = curCurveSet.GetArrayElementAtIndex(curveSetIndex);
                    Debug.Log(editorCurveSetName + " index : " + curveSetIndex + " attribute: " + curCurveInfo.FindPropertyRelative("attribute").stringValue);

                    var curCurve = curCurveInfo.FindPropertyRelative("curve");
                    var curCurveData = curCurve.FindPropertyRelative("m_Curve");
                    int curCurveDatalength = curCurveData.arraySize;
                    Debug.Log("curve lenght：" + curCurveDatalength);

                    for (int curveDataIndex = 3; curveDataIndex < curCurveDatalength; curveDataIndex++)
                    {
                        ProcessOneKeyframeOfEditorCurve(curveDataIndex, curCurveData, oneKeyframeTime);
                    }
                }
            }

            string[] curveSetNames = new string[] {"m_PositionCurves", "m_RotationCurves", "m_ScaleCurves", "m_FloatCurves"};

            foreach (var curveSetName in curveSetNames)
            {
                var curCurveSet = soClip.FindProperty(curveSetName);
                int curCurveSetLenght = curCurveSet.arraySize;
                if (curCurveSetLenght == 0)
                {
                    Debug.Log("Can not fine curves in " + curveSetName);
                    continue;
                }

                bool isHaveAttribute = curveSetName == "m_FloatCurves";

                for (int curveSetIndex = 0; curveSetIndex < curCurveSetLenght; curveSetIndex++)
                {
                    var curCurveInfo = curCurveSet.GetArrayElementAtIndex(curveSetIndex);
                    if (isHaveAttribute)
                    {
                        Debug.Log(curveSetName + " index : " + curveSetIndex + " attribute: " + curCurveInfo.FindPropertyRelative("attribute").stringValue);
                    }
                    else
                    {
                        Debug.Log(curveSetName + " index : " + curveSetIndex);
                    }

                    var curCurve = curCurveInfo.FindPropertyRelative("curve");
                    var curCurveData = curCurve.FindPropertyRelative("m_Curve");
                    int curCurveDatalength = curCurveData.arraySize;
                    Debug.Log("curve lenght：" + curCurveDatalength);

                    for (int curveDataIndex = 3; curveDataIndex < curCurveDatalength; curveDataIndex++)
                    {
                        ProcessOneKeyframeCurve(curveDataIndex, curCurveData, oneKeyframeTime);
                    }
                }
            }

            soClip.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        const int kBrokenMask = 1 << 0;
        const int kLeftTangentMask = 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4;
        const int kRightTangentMask = 1 << 5 | 1 << 6 | 1 << 7 | 1 << 8;

        static void SetKeyBroken(SerializedProperty keyframeInfo, bool broken)
        {
            var tangentModeProp = keyframeInfo.FindPropertyRelative("tangentMode");
            if (broken)
                tangentModeProp.intValue |= kBrokenMask;
            else
                tangentModeProp.intValue &= ~kBrokenMask;
        }

        static void SetKeyLeftTangentMode(SerializedProperty keyframeInfo, AnimationUtility.TangentMode tangentMode)
        {
            var tangentModeProp = keyframeInfo.FindPropertyRelative("tangentMode");
            tangentModeProp.intValue &= ~kLeftTangentMask;
            tangentModeProp.intValue |= (int)tangentMode << 1;
        }

        static void SetKeyRightTangentMode(SerializedProperty keyframeInfo, AnimationUtility.TangentMode tangentMode)
        {
            var tangentModeProp = keyframeInfo.FindPropertyRelative("tangentMode");
            tangentModeProp.intValue &= ~kRightTangentMask;
            tangentModeProp.intValue |= (int)tangentMode << 5;
        }

        static void ProcessOneKeyframeOfEditorCurve(int curveDataIndex, SerializedProperty curCurveData, float oneKeyframeTime)
        {
            var keyframe1 = curCurveData.GetArrayElementAtIndex(curveDataIndex - 3);
            var keyframe2 = curCurveData.GetArrayElementAtIndex(curveDataIndex - 2);
            var keyframe3 = curCurveData.GetArrayElementAtIndex(curveDataIndex - 1);
            var keyframe4 = curCurveData.GetArrayElementAtIndex(curveDataIndex);

            float time1 = keyframe1.FindPropertyRelative("time").floatValue;
            float time2 = keyframe2.FindPropertyRelative("time").floatValue;
            float time3 = keyframe3.FindPropertyRelative("time").floatValue;
            float time4 = keyframe4.FindPropertyRelative("time").floatValue;

            //check constant keyframe
            if ((time2 - time1 <= oneKeyframeTime) && (time3 - time2 <= oneKeyframeTime) && (time4 - time3 <= oneKeyframeTime))
            {
                var keyframeValue = keyframe1.FindPropertyRelative("value");

                Debug.Log(string.Format("index:{0},{1},{2},{3}", curveDataIndex - 3, curveDataIndex - 2, curveDataIndex - 1, curveDataIndex));

                SerializedProperty inSlope2 = keyframe2.FindPropertyRelative("inSlope");
                SerializedProperty inSlope3 = keyframe3.FindPropertyRelative("inSlope");
                SerializedProperty inSlope4 = keyframe4.FindPropertyRelative("inSlope");
                SerializedProperty outSlope1 = keyframe1.FindPropertyRelative("outSlope");
                SerializedProperty outSlope2 = keyframe2.FindPropertyRelative("outSlope");
                SerializedProperty outSlope3 = keyframe3.FindPropertyRelative("outSlope");

                switch (keyframeValue.propertyType)
                {
                    case SerializedPropertyType.Float:
                        SetKeyBroken(keyframe1, true);
                        SetKeyRightTangentMode(keyframe1, AnimationUtility.TangentMode.Linear);

                        SetKeyBroken(keyframe2, true);
                        SetKeyLeftTangentMode(keyframe2, AnimationUtility.TangentMode.Linear);
                        SetKeyRightTangentMode(keyframe2, AnimationUtility.TangentMode.Constant);
                        inSlope2.floatValue = float.PositiveInfinity;
                        outSlope2.floatValue = float.PositiveInfinity;

                        SetKeyBroken(keyframe3, true);
                        SetKeyLeftTangentMode(keyframe3, AnimationUtility.TangentMode.Constant);
                        SetKeyRightTangentMode(keyframe3, AnimationUtility.TangentMode.Linear);
                        inSlope3.floatValue = float.PositiveInfinity;
                        outSlope3.floatValue = float.PositiveInfinity;

                        SetKeyBroken(keyframe4, true);
                        SetKeyLeftTangentMode(keyframe4, AnimationUtility.TangentMode.Linear);
                        break;
                }
            }
        }

        static Vector3 ConstantVector3 = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        static Vector4 ConstantVector4 = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        static Quaternion ConstantQuaternion = new Quaternion(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, 1);

        static void ProcessOneKeyframeCurve(int curveDataIndex, SerializedProperty curCurveData, float oneKeyframeTime)
        {
            var keyframe1 = curCurveData.GetArrayElementAtIndex(curveDataIndex - 3);
            var keyframe2 = curCurveData.GetArrayElementAtIndex(curveDataIndex - 2);
            var keyframe3 = curCurveData.GetArrayElementAtIndex(curveDataIndex - 1);
            var keyframe4 = curCurveData.GetArrayElementAtIndex(curveDataIndex);

            float time1 = keyframe1.FindPropertyRelative("time").floatValue;
            float time2 = keyframe2.FindPropertyRelative("time").floatValue;
            float time3 = keyframe3.FindPropertyRelative("time").floatValue;
            float time4 = keyframe4.FindPropertyRelative("time").floatValue;

            //check constant keyframe
            if ((time2 - time1 <= oneKeyframeTime) && (time3 - time2 <= oneKeyframeTime) && (time4 - time3 <= oneKeyframeTime))
            {
                var keyframeValue = keyframe1.FindPropertyRelative("value");

                Debug.Log(string.Format("index:{0},{1},{2},{3}", curveDataIndex - 3, curveDataIndex - 2, curveDataIndex - 1, curveDataIndex));

                SerializedProperty inSlope2 = keyframe2.FindPropertyRelative("inSlope");
                SerializedProperty inSlope3 = keyframe3.FindPropertyRelative("inSlope");
                SerializedProperty inSlope4 = keyframe4.FindPropertyRelative("inSlope");
                SerializedProperty outSlope1 = keyframe1.FindPropertyRelative("outSlope");
                SerializedProperty outSlope2 = keyframe2.FindPropertyRelative("outSlope");
                SerializedProperty outSlope3 = keyframe3.FindPropertyRelative("outSlope");

                switch (keyframeValue.propertyType)
                {
                    case SerializedPropertyType.Float:
                        inSlope2.floatValue = float.PositiveInfinity;
                        outSlope2.floatValue = float.PositiveInfinity;
                        inSlope3.floatValue = float.PositiveInfinity;
                        outSlope3.floatValue = float.PositiveInfinity;
                        break;
                    case SerializedPropertyType.Vector3:
                        inSlope2.vector3Value = ConstantVector3;
                        outSlope2.vector3Value = ConstantVector3;
                        inSlope3.vector3Value = ConstantVector3;
                        outSlope3.vector3Value = ConstantVector3;
                        break;
                    case SerializedPropertyType.Vector4:
                        inSlope2.vector4Value = ConstantVector4;
                        outSlope2.vector4Value = ConstantVector4;
                        inSlope3.vector4Value = ConstantVector4;
                        outSlope3.vector4Value = ConstantVector4;
                        break;
                    case SerializedPropertyType.Quaternion:
                        inSlope2.quaternionValue = ConstantQuaternion;
                        outSlope2.quaternionValue = ConstantQuaternion;
                        inSlope3.quaternionValue = ConstantQuaternion;
                        outSlope3.quaternionValue = ConstantQuaternion;
                        break;
                }
            }
        }
    }
}