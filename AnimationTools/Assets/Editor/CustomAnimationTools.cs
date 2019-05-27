using UnityEngine;
using UnityEditor;
using System.IO;

namespace AnimationTools
{
    public class CustomAnimationTools
    {
        enum ConstantSlopTypes
        {
            None,
            InAndOutSlope,
            InSlope,
            OutSlope,
        }

        private static int number = 0;
        static System.DateTime StartTime;

        [MenuItem("AnimationTool/ProcessRotationInterpolationForConstant %e")]
        static void Execute()
        {
            number = 0;
            StartTime = System.DateTime.Now;

            UnityEngine.Object[] selectedObjects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.DeepAssets);

            foreach (UnityEngine.Object selectedObj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(selectedObj);
                if (selectedObj is AnimationClip)
                {
                    Debug.Log("is AnimationClip");
                    ProcessCurveForConstant(path);
                    number++;
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
                                number++;
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
            soClip.ApplyModifiedProperties();

            string[] curveSetNames = new string[] {"m_EditorCurves", "m_EulerEditorCurves" };

            foreach (var curveSetName in curveSetNames)
            {
                var curCurveSet = soClip.FindProperty(curveSetName);
                int curCurveSetLenght = curCurveSet.arraySize;
                if (curCurveSetLenght == 0)
                {
                    Debug.Log("Can not fine curves in " + curveSetName);
                    continue;
                }

                for (int curveSetIndex = 0; curveSetIndex < curCurveSetLenght; curveSetIndex++)
                {
                    var curCurveInfo = curCurveSet.GetArrayElementAtIndex(curveSetIndex);
                    Debug.Log(curveSetName + " index : " + curveSetIndex + " attribute: " + curCurveInfo.FindPropertyRelative("attribute").stringValue);

                    var curCurve = curCurveInfo.FindPropertyRelative("curve");
                    var curCurveData = curCurve.FindPropertyRelative("m_Curve");
                    int curCurveDatalength = curCurveData.arraySize;
                    Debug.Log("curve lenght：" + curCurveDatalength);

                    for (int curveDataIndex = 1; curveDataIndex < curCurveDatalength; curveDataIndex++)
                    {
                        var keyFrameInfo = curCurveData.GetArrayElementAtIndex(curveDataIndex);
                        ProcessOneKeyFrameForConstant(curveDataIndex, ref curCurveData, ref keyFrameInfo);
                    }
                }
            }
            soClip.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal class AnimationKeyframe
        {
            public float Time;
            public float Value;
            public float InSlope;
            public float OutSlope;
            public int TangentMode;
            private SerializedProperty _keyFrameInfo;

            public AnimationKeyframe(ref SerializedProperty keyFrameInfo)
            {
                _keyFrameInfo = keyFrameInfo;
                var timeProp = keyFrameInfo.FindPropertyRelative("time");
                Time = timeProp.floatValue;
                var valueProp = keyFrameInfo.FindPropertyRelative("value");
                Value = valueProp.floatValue;
                var inSlopeProp = keyFrameInfo.FindPropertyRelative("inSlope");
                InSlope = inSlopeProp.floatValue;
                var outSlopeProp = keyFrameInfo.FindPropertyRelative("outSlope");
                OutSlope = outSlopeProp.floatValue;
                var tangentModeProp = keyFrameInfo.FindPropertyRelative("tangentMode");
                TangentMode = tangentModeProp.intValue;
            }

            public override string ToString()
            {
                return string.Format("Time: {0}, Value:{1}, InSlope:{2}, OutSlope:{3}, TangetMode:{4}", Time, Value, InSlope, OutSlope, TangentMode);
            }

            public void Save()
            {
                _keyFrameInfo.FindPropertyRelative("time").floatValue = Time;
                _keyFrameInfo.FindPropertyRelative("value").floatValue = Value;
                _keyFrameInfo.FindPropertyRelative("inSlope").floatValue = InSlope;
                _keyFrameInfo.FindPropertyRelative("outSlope").floatValue = OutSlope;
                _keyFrameInfo.FindPropertyRelative("tangentMode").intValue = TangentMode;
            }
        }

        const int kBrokenMask = 1 << 0;
        const int kLeftTangentMask = 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4;
        const int kRightTangentMask = 1 << 5 | 1 << 6 | 1 << 7 | 1 << 8;

        static void SetKeyBroken(AnimationKeyframe akf, bool broken)
        {
            if (broken)
                akf.TangentMode |= kBrokenMask;
            else
                akf.TangentMode &= ~kBrokenMask;
        }

        static void SetKeyLeftTangentMode(AnimationKeyframe akf, AnimationUtility.TangentMode tangentMode)
        {
            akf.TangentMode &= ~kLeftTangentMask;
            akf.TangentMode |= (int)tangentMode << 1;
        }

        static void SetKeyRightTangentMode(AnimationKeyframe akf, AnimationUtility.TangentMode tangentMode)
        {
            akf.TangentMode &= ~kRightTangentMask;
            akf.TangentMode |= (int)tangentMode << 5;
        }

        static void ProcessOneKeyFrameForConstant(int curveDataIndex, ref SerializedProperty curCurveData, ref SerializedProperty keyFrameInfo)
        {
            var prevKeyFrameInfo = curCurveData.GetArrayElementAtIndex(curveDataIndex - 1);
            AnimationKeyframe prevKeyframe = new AnimationKeyframe(ref prevKeyFrameInfo);

            AnimationKeyframe keyframe = new AnimationKeyframe(ref keyFrameInfo);
            Debug.Log(curveDataIndex + " " + keyframe.ToString());
            if(keyframe.Time - prevKeyframe.Time <= 0.034f)
            {
                Debug.LogWarning("need process: " + keyframe.Time + " " + prevKeyframe.Time);

                prevKeyframe.OutSlope = float.PositiveInfinity;
                SetKeyBroken(prevKeyframe, true);
                SetKeyRightTangentMode(prevKeyframe, AnimationUtility.TangentMode.Constant);
                prevKeyframe.Save();

                SetKeyBroken(keyframe, true);
                SetKeyLeftTangentMode(keyframe, AnimationUtility.TangentMode.Constant);
                keyframe.InSlope = float.PositiveInfinity;
                keyframe.Save();
            }
        }



        static void ProcessRotationInterpolationForConstant(AnimationClip clip, string clipName)
        {
            var soClip = new SerializedObject(clip);
            var eulerEditorCurves = soClip.FindProperty("m_EulerEditorCurves");
            int len = eulerEditorCurves.arraySize;
            if (len == 0)
            {
                Debug.Log("No euler editor curves, please edit animation in Unity first.");
                return;
            }

            AnimationCurve eulerEditorCurveX = new AnimationCurve();
            AnimationCurve eulerEditorCurveY = new AnimationCurve();
            AnimationCurve eulerEditorCurveZ = new AnimationCurve();

            for (int i = 0; i < len; i++)
            {
                var curveInfo = eulerEditorCurves.GetArrayElementAtIndex(i);
                var attributeProp = curveInfo.FindPropertyRelative("attribute");
                var attributeName = attributeProp.stringValue;

                var animationCurveProp = curveInfo.FindPropertyRelative("curve");
                if (attributeName.EndsWith(".x"))
                {
                    eulerEditorCurveX = animationCurveProp.animationCurveValue;
                }
                if (attributeName.EndsWith(".y"))
                {
                    eulerEditorCurveY = animationCurveProp.animationCurveValue;
                }
                if (attributeName.EndsWith(".z"))
                {
                    eulerEditorCurveZ = animationCurveProp.animationCurveValue;
                }
            }

            var rotationCurves = soClip.FindProperty("m_RotationCurves");
            var rotationCurveInfo = rotationCurves.GetArrayElementAtIndex(0);
            var rotationCurveProp = rotationCurveInfo.FindPropertyRelative("curve");
            var rotationCurveArrayProp = rotationCurveProp.FindPropertyRelative("m_Curve");
            int length = rotationCurveArrayProp.arraySize;
            //Debug.Log("动画采样帧数：" + length);

            for (int index = 0; index < length; index++)
            {
                var curveInfo = rotationCurveArrayProp.GetArrayElementAtIndex(index);
                var timeProp = curveInfo.FindPropertyRelative("time");
                float time = timeProp.floatValue;
                ProcessConstant(eulerEditorCurveX, eulerEditorCurveY, eulerEditorCurveZ, time, ref curveInfo);
            }
            soClip.ApplyModifiedProperties();

        }

        static ConstantSlopTypes CheckConstantSlopType(AnimationCurve ac, float time, ref SerializedProperty curveInfo)
        {
            ConstantSlopTypes ret = ConstantSlopTypes.None;
            int len = ac.keys.Length;
            int leftIndex = 0;
            int rightIndex = 0;
            Keyframe[] keys = ac.keys;

            for (int i = 0; i < len; i++)
            {
                if (keys[i].time - time > 0.0001f)
                {
                    rightIndex = i;
                    break;
                }
                leftIndex = i;
                if (Mathf.Abs(keys[i].time - time) < 0.0001f)
                {
                    rightIndex = i;
                    break;
                }
            }

            AnimationUtility.TangentMode outTM = AnimationUtility.GetKeyRightTangentMode(ac, leftIndex);
            AnimationUtility.TangentMode inTM = AnimationUtility.GetKeyLeftTangentMode(ac, rightIndex);

            if (leftIndex == rightIndex)
            {
                if (inTM == AnimationUtility.TangentMode.Constant)
                {
                    ret = ConstantSlopTypes.InSlope;
                }
                if (outTM == AnimationUtility.TangentMode.Constant)
                {
                    if (ret == ConstantSlopTypes.InSlope)
                    {
                        ret = ConstantSlopTypes.InAndOutSlope;
                    }
                    else
                    {
                        ret = ConstantSlopTypes.OutSlope;
                    }
                }
            }
            else
            {
                if (outTM == AnimationUtility.TangentMode.Constant
                    || inTM == AnimationUtility.TangentMode.Constant)
                {
                    ret = ConstantSlopTypes.InAndOutSlope;
                }
            }
            return ret;
        }

        static void ProcessConstant(
            AnimationCurve acX,
            AnimationCurve acY,
            AnimationCurve acZ,
            float time, ref SerializedProperty curveInfo)
        {
            ConstantSlopTypes cstX = CheckConstantSlopType(acX, time, ref curveInfo);
            ConstantSlopTypes cstY = CheckConstantSlopType(acY, time, ref curveInfo);
            ConstantSlopTypes cstZ = CheckConstantSlopType(acZ, time, ref curveInfo);

            SerializedProperty inSlope = curveInfo.FindPropertyRelative("inSlope");
            Quaternion frameValue = inSlope.quaternionValue;
            if (Mathf.Abs(frameValue.x + frameValue.y + frameValue.z + frameValue.w) < 0.0001f)
            {
                //blank
            }
            else
            {
                bool isDirty = false;
                if (cstX == ConstantSlopTypes.InSlope || cstX == ConstantSlopTypes.InAndOutSlope)
                {
                    frameValue.x = float.PositiveInfinity;
                    isDirty = true;
                }
                if (cstY == ConstantSlopTypes.InSlope || cstY == ConstantSlopTypes.InAndOutSlope)
                {
                    frameValue.y = float.PositiveInfinity;
                    isDirty = true;
                }
                if (cstZ == ConstantSlopTypes.InSlope || cstZ == ConstantSlopTypes.InAndOutSlope)
                {
                    frameValue.z = float.PositiveInfinity;
                    isDirty = true;
                }
                if (isDirty)
                {
                    inSlope.quaternionValue = frameValue;
                    //Debug.Log("inSlope time: " + time + " " + frameValue);
                }
            }

            SerializedProperty outSlope = curveInfo.FindPropertyRelative("outSlope");
            frameValue = outSlope.quaternionValue;
            if (Mathf.Abs(frameValue.x + frameValue.y + frameValue.z + frameValue.w) < 0.0001f)
            {
                //blank
            }
            else
            {
                bool isDirty = false;
                if (cstX == ConstantSlopTypes.OutSlope || cstX == ConstantSlopTypes.InAndOutSlope)
                {
                    frameValue.x = float.PositiveInfinity;
                    isDirty = true;
                }
                if (cstY == ConstantSlopTypes.OutSlope || cstY == ConstantSlopTypes.InAndOutSlope)
                {
                    frameValue.y = float.PositiveInfinity;
                    isDirty = true;
                }
                if (cstZ == ConstantSlopTypes.OutSlope || cstZ == ConstantSlopTypes.InAndOutSlope)
                {
                    frameValue.z = float.PositiveInfinity;
                    isDirty = true;
                }
                if (isDirty)
                {
                    outSlope.quaternionValue = frameValue;
                    //Debug.Log("outSlope time: " + time + " " + frameValue);
                }
            }
        }

    }
}