namespace Registry.Domain.Agents;

// insight - an agent record has no lifecycle yet (it is created, never transitioned), so no state machine;
// suspension of an agent's access is a grant concern and lives in Review
public enum AgentActionType
{
    Register
}
