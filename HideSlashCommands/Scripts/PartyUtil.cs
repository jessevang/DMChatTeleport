namespace DMChatTeleports
{
    public static class PartyUtil
    {
        // Returns PartyID or 0 if not in a party / unknown
        public static int TryGetPartyIdForEntity(EntityAlive killer)
        {
            try
            {
                if (killer is EntityPlayer ep && ep.Party != null)
                {
                    int id = ep.Party.PartyID;
                    return id > 0 ? id : 0;
                }
            }
            catch { }

            return 0;
        }
    }
}
