using System;
using System.Linq;

namespace CuboidFever;

// The row of placeable blocks and which slot is selected.
public sealed class Hotbar
{
    private readonly byte[] _ids;
    private readonly string[] _names;
    private readonly ActionInput _input;
    private readonly BlockRegistry _blocks;

    public Hotbar(BlockRegistry blocks, ActionInput input)
    {
        _input = input;
        _blocks = blocks;
        _ids = blocks.HotbarIds;
        _names = _ids
            .Select(id => blocks.GetBaseName(id)
                .Split(':').Last()
                .Replace('_', ' '))
            .ToArray();

        Slot = Math.Min(2, _ids.Length - 1); // start on the third block, as before
    }

    public int Slot { get; private set; }
    public int Count => _ids.Length;
    public byte SelectedId => _ids[Slot];
    public string SelectedName => _names[Slot];

    public bool TrySelectBlock(byte blockId)
    {
        // Every state of a block (an open door facing east, say) picks the hotbar slot of its default state
        blockId = _blocks.GetDefaultState(blockId);

        for (int i = 0; i < _ids.Length; i++)
        {
            if (_ids[i] == blockId)
            {
                Slot = i;
                return true;
            }
        }
        return false; // Block is not currently in the hotbar
    }

    // Selects a slot by number, for restoring a saved game. A number outside the hotbar is ignored.
    public void SelectSlot(int slot)
    {
        if (slot >= 0 && slot < _ids.Length) Slot = slot;
    }

    // Number keys jump to a slot, the mouse wheel scrolls through them.
    public void Update()
    {
        // GameAction.Hotbar1..Hotbar9 are declared in order, so they can be offset from Hotbar1
        for (int i = 0; i < Math.Min(9, _ids.Length); i++)
            if (_input.WasPressed(GameAction.Hotbar1 + i)) Slot = i;

        int scroll = _input.ScrollDelta;
        if (scroll != 0)
            Slot = ((Slot - Math.Sign(scroll)) % _ids.Length + _ids.Length) % _ids.Length;
    }
}
