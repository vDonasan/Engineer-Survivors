namespace ES.Sim
{
    /// <summary>Entrada do jogador em 8 bytes.</summary>
    public static class InputCodec
    {
        public const int Size = 8;

        public static void Write(ByteWriter w, in PlayerInput input)
        {
            w.U16(input.Seq);
            w.I8(input.MoveX);
            w.I8(input.MoveY);
            w.U8(input.SpecialCounter);
            w.I16(input.ChoiceOfferId);
            w.I8(input.ChoiceIndex);
        }

        public static bool TryRead(ref ByteReader r, out PlayerInput input)
        {
            input = PlayerInput.None;
            if (r.Remaining < Size) return false;
            r.TryU16(out input.Seq);
            r.TryI8(out input.MoveX);
            r.TryI8(out input.MoveY);
            r.TryU8(out input.SpecialCounter);
            r.TryI16(out input.ChoiceOfferId);
            r.TryI8(out input.ChoiceIndex);
            return true;
        }
    }
}
