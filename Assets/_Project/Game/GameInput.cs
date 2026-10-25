using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ES.Game
{
    /// <summary>Ações do jogo criadas em código; a tecla do especial é reatribuível.</summary>
    public sealed class GameInput : IDisposable
    {
        public readonly InputAction Move, Special, Pause, Perf, Choice1, Choice2, Choice3, DebugSpawn;
        InputActionRebindingExtensions.RebindingOperation _rebind;

        public GameInput(string specialBinding = SettingsStore.DefaultSpecialBinding)
        {
            Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Special = new InputAction("Special", InputActionType.Button, specialBinding);
            Pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Perf = new InputAction("Perf", InputActionType.Button, "<Keyboard>/f3");
            Choice1 = new InputAction("Choice1", InputActionType.Button, "<Keyboard>/1");
            Choice2 = new InputAction("Choice2", InputActionType.Button, "<Keyboard>/2");
            Choice3 = new InputAction("Choice3", InputActionType.Button, "<Keyboard>/3");
            DebugSpawn = new InputAction("DebugSpawn", InputActionType.Button, "<Keyboard>/f4");
            foreach (var a in All()) a.Enable();
        }

        InputAction[] All() => new[] { Move, Special, Pause, Perf, Choice1, Choice2, Choice3, DebugSpawn };

        public Vector2 MoveValue => Move.ReadValue<Vector2>();

        public string SpecialPath => Special.bindings[0].effectivePath;

        public void SetSpecialBinding(string path)
        {
            if (string.IsNullOrEmpty(path)) path = SettingsStore.DefaultSpecialBinding;
            Special.ApplyBindingOverride(0, path);
        }

        public bool Rebinding => _rebind != null;

        /// <summary>Espera a próxima tecla para o especial (Esc cancela). Chama <paramref name="done"/> com o caminho ou null.</summary>
        public void RebindSpecial(Action<string> done)
        {
            CancelRebind();
            Special.Disable();
            _rebind = Special.PerformInteractiveRebinding(0)
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Pointer>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0f)
                .OnComplete(op => Finish(done, Special.bindings[0].effectivePath))
                .OnCancel(op => Finish(done, null))
                .Start();
        }

        void Finish(Action<string> done, string path)
        {
            _rebind?.Dispose();
            _rebind = null;
            Special.Enable();
            done?.Invoke(path);
        }

        public void CancelRebind()
        {
            if (_rebind == null) return;
            _rebind.Cancel();
        }

        /// <summary>Nome legível da tecla ("Espaço", "F", ...).</summary>
        public string SpecialDisplayName()
        {
            string s = Special.GetBindingDisplayString(0);
            return string.IsNullOrEmpty(s) ? "?" : s;
        }

        public void Dispose()
        {
            CancelRebind();
            foreach (var a in All()) a.Dispose();
        }
    }
}
