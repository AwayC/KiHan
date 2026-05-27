using KiHan.Logic;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


public class NarutoStateIdle : CommonStateIdle
{
    public override void Update(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero; // 确保静止

        var input = owner.CurrInput;
        if (input == null) return;

        // 恢复为简单的状态检查：只要按着攻击键就进入攻击状态
        if ((input.Buttons & ButtonMask.Skill1) != 0) {
            Debug.Log("skill1 btn");
            owner.RootSM.ChangeState(NarutoState.SkillA);
            return;
        }

        if ((input.Buttons & ButtonMask.Skill2) != 0)
        {
            Debug.Log("skill2 btn");
            owner.RootSM.ChangeState(NarutoState.SkillB);
            return;
        }

        if ((input.Buttons & ButtonMask.Attack) != 0)
        {
            Debug.Log("attack btn");
            owner.RootSM.ChangeState(CommonState.Attack);
            return;
        }

        if (input.JoyStickAngle != 255)
        {
            Debug.Log("run");
            owner.RootSM.ChangeState(CommonState.Run);
        }
    }
}