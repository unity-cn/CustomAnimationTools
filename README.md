# CustomAnimationTools

Fix interpolation of constant on Unity 2018.4.0f1.
Rotation curves are tested.

You can select *.anim file or select animation clips of FBX.

1.Select animtion in Project window

2.Click menu: AnimationTool->ProcessForConstant

3.Done.

Animation in Maya
![Correct curve](https://github.com/unity-cn/CustomAnimationTools/blob/master/AnimationTools/Assets/TestCase2/_correct_curve.png)

---

FBX imported into Unity. FBX settings are default. But this curve is wrong.
![Wrong curve](https://github.com/unity-cn/CustomAnimationTools/blob/master/AnimationTools/Assets/TestCase2/_wrong_curve.jpg)

---
Animation result after processed.
![Result curve](https://github.com/unity-cn/CustomAnimationTools/blob/master/AnimationTools/Assets/TestCase2/_result_curve.png)

Notice:
1.Keyframes must be full in animation. Please try bake keys on every frame.
