using System.Runtime.InteropServices;
namespace BatteryDisplay;

static class Controllers
{
    [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index, byte[] state);
    [DllImport("xinput1_4.dll")] static extern uint XInputGetBatteryInformation(uint index, byte deviceType, byte[] battery);
    public static IEnumerable<Reading> Scan()
    {
        for (uint i = 0; i < 4; i++)
        {
            if (XInputGetState(i, new byte[16]) != 0) continue;
            var battery = new byte[2]; var status = "Подключён через XInput • процент недоступен";
            if (XInputGetBatteryInformation(i, 0, battery) == 0)
            {
                status = battery[0] switch { 1 => "XInput сообщает проводное питание • процент недоступен", 2 or 3 => "XInput • заряд: " + (battery[1] switch { 0 => "пусто", 1 => "низкий", 2 => "средний", 3 => "высокий", _ => "неизвестен" }), _ => status };
            }
            yield return new Reading("xinput:" + i, "Геймпад XInput " + (i + 1), null, "Геймпад", status, LinkState.Connected);
        }
    }
}


