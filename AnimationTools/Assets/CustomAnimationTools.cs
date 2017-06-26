using UnityEngine;
using UnityEditor;


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
	static System.DateTime time;

    [MenuItem("AnimationTool/ProcessRotationInterpolationForConstant")]
	static void Execute()
	{
		number = 0;
        time = System.DateTime.Now;
		foreach (UnityEngine.Object o in Selection.GetFiltered(typeof(AnimationClip), SelectionMode.DeepAssets))
		{
			number++;
            string path = AssetDatabase.GetAssetPath(o);
            ProcessRotationInterpolationForConstant(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), path);
		}
		AssetDatabase.SaveAssets();
        Debug.Log("耗时:"+((System.DateTime.Now-time).TotalMilliseconds/1000)+"秒.");
	}


	static void ProcessRotationInterpolationForConstant(AnimationClip clip, string clipName)
	{
        var soClip = new SerializedObject(clip);
        var eulerEditorCurves = soClip.FindProperty("m_EulerEditorCurves");
        int len = eulerEditorCurves.arraySize;
        if(len == 0)
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
            if(attributeName.EndsWith(".x"))
            {
                eulerEditorCurveX = animationCurveProp.animationCurveValue;
            }
            if(attributeName.EndsWith(".y"))
            {
                eulerEditorCurveY = animationCurveProp.animationCurveValue;
            }
            if(attributeName.EndsWith(".z"))
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

        for(int index = 0; index < length; index ++)
        {
            var curveInfo = rotationCurveArrayProp.GetArrayElementAtIndex(index);
            var timeProp = curveInfo.FindPropertyRelative("time");
            float time = timeProp.floatValue;
            ProcessConstant(eulerEditorCurveX, eulerEditorCurveY, eulerEditorCurveZ, time, ref curveInfo);
        }
        soClip.ApplyModifiedProperties();

	}

    private static int kLeftTangentMask = 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4;
    private static int kRightTangentMask = 1 << 5 | 1 << 6 | 1 << 7 | 1 << 8;

    private static AnimationUtility.TangentMode GetKeyLeftTangentMode(Keyframe key)
    {
        return (AnimationUtility.TangentMode)((key.tangentMode & kLeftTangentMask) >> 1);
    }

    private static AnimationUtility.TangentMode GetKeyRightTangentMode(Keyframe key)
    {
        return (AnimationUtility.TangentMode)((key.tangentMode & kRightTangentMask) >> 5);
    }

    static ConstantSlopTypes CheckConstantSlopType(AnimationCurve ac, float time, ref SerializedProperty curveInfo)
    {
        ConstantSlopTypes ret = ConstantSlopTypes.None;
        int len = ac.keys.Length;
        int leftIndex = 0;
        int rightIndex = 0;
        Keyframe[] keys = ac.keys;

        for(int i = 0; i < len; i ++)
        {
            if(keys[i].time - time > 0.0001f)
            {
                rightIndex = i;
                break;
            }
            leftIndex = i;
            if(Mathf.Abs(keys[i].time - time) < 0.0001f)
            {
                rightIndex = i;
                break;
            }
        }

        AnimationUtility.TangentMode outTM = GetKeyRightTangentMode(keys[leftIndex]);
        AnimationUtility.TangentMode inTM = GetKeyLeftTangentMode(keys[rightIndex]);

        if(leftIndex == rightIndex)
        {
            if(inTM == AnimationUtility.TangentMode.Constant)
            {
                ret = ConstantSlopTypes.InSlope;
            }
            if(outTM == AnimationUtility.TangentMode.Constant)
            {
                if(ret == ConstantSlopTypes.InSlope)
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
            if(outTM == AnimationUtility.TangentMode.Constant
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
        if(Mathf.Abs( frameValue.x + frameValue.y + frameValue.z + frameValue.w) < 0.0001f)
        {
            //blank
        }
        else
        {
            bool isDirty = false;
            if(cstX == ConstantSlopTypes.InSlope || cstX == ConstantSlopTypes.InAndOutSlope)
            {
                frameValue.x = float.PositiveInfinity;
                isDirty = true;
            }
            if(cstY == ConstantSlopTypes.InSlope || cstY == ConstantSlopTypes.InAndOutSlope)
            {
                frameValue.y = float.PositiveInfinity;
                isDirty = true;
            }
            if(cstZ == ConstantSlopTypes.InSlope || cstZ == ConstantSlopTypes.InAndOutSlope)
            {
                frameValue.z = float.PositiveInfinity;
                isDirty = true;
            }
            if(isDirty)
            {
                inSlope.quaternionValue = frameValue;
                //Debug.Log("inSlope time: " + time + " " + frameValue);
            }
        }

        SerializedProperty outSlope = curveInfo.FindPropertyRelative("outSlope");
        frameValue = outSlope.quaternionValue;
        if(Mathf.Abs( frameValue.x + frameValue.y + frameValue.z + frameValue.w) < 0.0001f)
        {
            //blank
        }
        else
        {
            bool isDirty = false;
            if(cstX == ConstantSlopTypes.OutSlope || cstX == ConstantSlopTypes.InAndOutSlope)
            {
                frameValue.x = float.PositiveInfinity;
                isDirty = true;
            }
            if(cstY == ConstantSlopTypes.OutSlope || cstY == ConstantSlopTypes.InAndOutSlope)
            {
                frameValue.y = float.PositiveInfinity;
                isDirty = true;
            }
            if(cstZ == ConstantSlopTypes.OutSlope || cstZ == ConstantSlopTypes.InAndOutSlope)
            {
                frameValue.z = float.PositiveInfinity;
                isDirty = true;
            }
            if(isDirty)
            {
                outSlope.quaternionValue = frameValue;
                //Debug.Log("outSlope time: " + time + " " + frameValue);
            }
        }
    }

}