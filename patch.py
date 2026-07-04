import re

path = 'Assets/Project/Scripts/Player/States/CombatPlayerState.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace FinishActiveActionSequence
target_finish = r'''    private void FinishActiveActionSequence\(\)
    \{
        if \(activeActionSequenceCoroutine != null && player != null\)
        \{
            player\.StopCoroutine\(activeActionSequenceCoroutine\);
            activeActionSequenceCoroutine = null;
        \}

        if \(activeFreezeOverrideCoroutine != null && player != null\)
        \{
            player\.StopCoroutine\(activeFreezeOverrideCoroutine\);
            activeFreezeOverrideCoroutine = null;
        \}

        if \(GameFreezeManager\.Instance != null && activeFreezeToken\.IsValid\)
        \{
            GameFreezeManager\.Instance\.EndScopedFreeze\(activeFreezeToken\);
        \}

        if \(CameraManager\.instance != null\)
        \{
            CameraManager\.instance\.EndTemporaryPlayerSnap\(\);
        \}

        // Restore animator update mode to normal after the sequence finishes
        if \(player\.Animator != null && player\.Animator\.anim != null\)
        \{
            player\.Animator\.anim\.updateMode = AnimatorUpdateMode\.Normal;
        \}

        activeFreezeToken = default;
        activeActionSequence = null;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        player\.Movement\?\.SetUseUnscaledAttackMotion\(false\);
    \}'''

repl_finish = r'''    private void FinishActiveActionSequence()
    {
        if (activeActionSequenceCoroutine != null && player != null)
        {
            player.StopCoroutine(activeActionSequenceCoroutine);
            activeActionSequenceCoroutine = null;
        }

        if (activeFreezeOverrideCoroutine != null)
        {
            if (CameraManager.instance != null)
            {
                CameraManager.instance.EndTemporaryPlayerSnap();
            }
            return;
        }

        if (GameFreezeManager.Instance != null && activeFreezeToken.IsValid)
        {
            GameFreezeManager.Instance.EndScopedFreeze(activeFreezeToken);
        }

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EndTemporaryPlayerSnap();
        }

        // Restore animator update mode to normal after the sequence finishes
        if (player.Animator != null && player.Animator.anim != null)
        {
            player.Animator.anim.updateMode = AnimatorUpdateMode.Normal;
        }

        activeFreezeToken = default;
        activeActionSequence = null;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        player.Movement?.SetUseUnscaledAttackMotion(false);
    }'''

content = re.sub(target_finish, repl_finish, content)

# Replace EndFreezeAfterDelay
target_end = r'''    private System\.Collections\.IEnumerator EndFreezeAfterDelay\(float delay, GameFreezeManager\.FreezeToken token\)
    \{
        yield return new WaitForSecondsRealtime\(delay\);
        if \(GameFreezeManager\.Instance != null && token\.IsValid\)
        \{
            GameFreezeManager\.Instance\.EndScopedFreeze\(token\);
        \}
        activeFreezeOverrideCoroutine = null;
    \}'''

repl_end = r'''    private System.Collections.IEnumerator EndFreezeAfterDelay(float delay, GameFreezeManager.FreezeToken token)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (GameFreezeManager.Instance != null && token.IsValid)
        {
            GameFreezeManager.Instance.EndScopedFreeze(token);
        }
        activeFreezeOverrideCoroutine = null;

        if (player != null && player.Animator != null && player.Animator.anim != null)
        {
            player.Animator.anim.updateMode = AnimatorUpdateMode.Normal;
        }
        activeFreezeToken = default;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        if (player != null && player.Movement != null)
        {
            player.Movement.SetUseUnscaledAttackMotion(false);
        }
    }'''

content = re.sub(target_end, repl_end, content)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print('Done')
