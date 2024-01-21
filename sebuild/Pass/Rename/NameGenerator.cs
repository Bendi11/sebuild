
using System.Text;

namespace SeBuild.Pass.Rename;

/// <summary>
/// Generate random unicode names using a list of known valid characters
/// </summary>
struct NameGenerator {
    List<IEnumerator<char>> _gen = new List<IEnumerator<char>>();

    public NameGenerator() {
        _gen.Add(UnicodeEnumerator());
    }

    private void IncrementSlot(int slot) {
        if(slot >= _gen.Count) {
            _gen.Append(UnicodeEnumerator());
            return;
        }

        if(!_gen[slot].MoveNext()) {
            _gen[slot] = UnicodeEnumerator();
            IncrementSlot(slot + 1);
        }
    }

    public string Next() {
        StringBuilder sb = new StringBuilder();
        
        IncrementSlot(0);
        foreach(var slot in _gen) {
            sb.Append(slot.Current);
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Get a new iterator over all valid unicode code points that can be used for a single character in a generated identifier
    /// </summary>
    private IEnumerator<char> UnicodeEnumerator() {
        foreach(var (low, high) in VALID_CODES) {
            for(int i = low; i <= high; ++i) {
                yield return (char)i;
            }
        }
    }

    private static readonly (int, int)[] VALID_CODES = {
        (0x41,   0x5A),
        (0x61,   0x7A),
        (0xC0,   0xD6),
        (0xD8,   0xF6),
        (0x100,  0x17F),
        (0x180,  0x1BF),
        (0x1C4,  0x1CC),
        (0x1CD,  0x1DC),
        (0x1DD,  0x1FF),
        (0x200,  0x217),
        (0x218,  0x21B),
        (0x21C,  0x24F),
        (0x22A,  0x233),
        (0x234,  0x236),
        (0x238,  0x240),
        (0x23A,  0x23E),
        (0x250,  0x2A8),
        (0x2A9,  0x2AD),
        (0x2AE,  0x2AF),
        (0x370,  0x3FB),
        (0x37B,  0x37D),
        (0x37F,  0x3F3),
        (0x3CF,  0x3F9),
        (0x3E2,  0x3EF),
        (0x400,  0x45F),
        (0x410,  0x44F),
        (0x460,  0x481),
        (0x48A,  0x4F9),
        (0x4FA,  0x4FF),
        (0x500,  0x52D),
        (0x531,  0x556),
        (0x560,  0x588),
        (0x10A0, 0x10C5),
        (0x10D0, 0x10F0),
        (0x13A0, 0x13F4),
        (0x1C90, 0x1CB0),
        (0x1E00, 0x1EF9),
        (0x1EA0, 0x1EF1),
        (0x1F00, 0x1FFC),
        (0x2C00, 0x2C2E),
        (0x2C30, 0x2C5E),
    };
}
