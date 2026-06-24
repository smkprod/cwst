using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/tournaments")]
public class TournamentsController(
    CreateTournamentUseCase create,
    JoinTournamentUseCase join,
    LeaveTournamentUseCase leave,
    UpdateTournamentUseCase update,
    GenerateTournamentBracketUseCase generateBracket,
    SetTournamentMatchResultUseCase setResult,
    CancelTournamentUseCase cancel,
    GetTournamentUseCase getOne,
    GetTournamentListUseCase getList,
    IConfiguration config) : ControllerBase
{
    public record CreateRequest(string Name, string? Description, string? PrizeInfo,
        string ClanInviteLink, int BestOf, int MaxParticipants);
    public record UpdateRequest(string Name, string? Description, string? PrizeInfo,
        string ClanInviteLink, int BestOf, int MaxParticipants);
    public record SetResultRequest(int ScoreA, int ScoreB);

    /// <summary>GET /api/tournaments — открытые/идущие турниры, для вкладки "Турнир".</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await getList.ExecuteAsync(ct));

    /// <summary>GET /api/tournaments/{id} — детали турнира с сеткой.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var dto = await getOne.ExecuteAsync(id, userId, ct);
        return dto is null ? NotFound(new { error = "tournament_not_found" }) : Ok(dto);
    }

    /// <summary>POST /api/tournaments — создать турнир (создатель автоматически становится участником).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var (tournament, error) = await create.ExecuteAsync(
            userId, req.Name, req.Description, req.PrizeInfo, req.ClanInviteLink, req.BestOf, req.MaxParticipants, ct);

        if (error is not null) return MapCreateError(error.Value);
        var dto = await getOne.ExecuteAsync(tournament!.Id, userId, ct);
        return Ok(dto);
    }

    /// <summary>PUT /api/tournaments/{id} — редактирование турнира (только создатель).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await update.ExecuteAsync(
            id, userId, req.Name, req.Description, req.PrizeInfo, req.ClanInviteLink, req.BestOf, req.MaxParticipants, ct);
        if (error is not null) return MapUpdateError(error.Value);

        return Ok(await getOne.ExecuteAsync(id, userId, ct));
    }

    /// <summary>POST /api/tournaments/{id}/join — вступить в турнир.</summary>
    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await join.ExecuteAsync(id, userId, ct);
        if (error is not null) return MapJoinError(error.Value);
        return Ok(await getOne.ExecuteAsync(id, userId, ct));
    }

    /// <summary>POST /api/tournaments/{id}/leave — выйти из турнира (до жеребьёвки).</summary>
    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await leave.ExecuteAsync(id, userId, ct);
        if (error is not null) return MapLeaveError(error.Value);
        return Ok(await getOne.ExecuteAsync(id, userId, ct));
    }

    /// <summary>POST /api/tournaments/{id}/bracket — сгенерировать/перестроить сетку (только создатель).</summary>
    [HttpPost("{id:int}/bracket")]
    public async Task<IActionResult> GenerateBracket(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await generateBracket.ExecuteAsync(id, userId, ct);
        if (error is not null) return MapBracketError(error.Value);
        return Ok(await getOne.ExecuteAsync(id, userId, ct));
    }

    /// <summary>POST /api/tournaments/{id}/matches/{matchId}/result — внести/исправить результат (только создатель).</summary>
    [HttpPost("{id:int}/matches/{matchId:int}/result")]
    public async Task<IActionResult> SetResult(int id, int matchId, [FromBody] SetResultRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await setResult.ExecuteAsync(id, matchId, userId, req.ScoreA, req.ScoreB, ct);
        if (error is not null) return MapResultError(error.Value);
        return Ok(await getOne.ExecuteAsync(id, userId, ct));
    }

    /// <summary>DELETE /api/tournaments/{id} — отменить турнир (создатель или владелец сервиса).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await cancel.ExecuteAsync(id, userId, IsOwner(userId), ct);
        if (error is not null) return MapCancelError(error.Value);
        return Ok(new { ok = true });
    }

    private bool IsOwner(long userId) =>
        long.TryParse(config["Owner:TelegramUserId"], out var ownerId) && ownerId != 0 && userId == ownerId;

    private IActionResult MapCreateError(CreateTournamentError e) => e switch
    {
        CreateTournamentError.PlayerNotLinked => NotFound(new { error = "player_not_linked" }),
        CreateTournamentError.TooManyActive => StatusCode(429, new { error = "too_many_active", message = "Слишком много активных турниров — заверши или отмени старые" }),
        CreateTournamentError.BadLink => BadRequest(new { error = "bad_link", message = "Нужна ссылка-приглашение в клан (clashroyale.com)" }),
        CreateTournamentError.BadName => BadRequest(new { error = "bad_name", message = "Название турнира: 1–80 символов" }),
        CreateTournamentError.BadFormat => BadRequest(new { error = "bad_format" }),
        _ => BadRequest(new { error = "bad_request" }),
    };

    private IActionResult MapUpdateError(UpdateTournamentError e) => e switch
    {
        UpdateTournamentError.TournamentNotFound => NotFound(new { error = "tournament_not_found" }),
        UpdateTournamentError.NotCreator => StatusCode(403, new { error = "not_creator" }),
        UpdateTournamentError.BadName => BadRequest(new { error = "bad_name" }),
        UpdateTournamentError.BadLink => BadRequest(new { error = "bad_link" }),
        UpdateTournamentError.BadFormat => BadRequest(new { error = "bad_format" }),
        UpdateTournamentError.AlreadyStarted => BadRequest(new { error = "already_started", message = "Формат турнира меняется только до жеребьёвки" }),
        _ => BadRequest(new { error = "bad_request" }),
    };

    private IActionResult MapJoinError(JoinTournamentError e) => e switch
    {
        JoinTournamentError.PlayerNotLinked => NotFound(new { error = "player_not_linked" }),
        JoinTournamentError.TournamentNotFound => NotFound(new { error = "tournament_not_found" }),
        JoinTournamentError.NotOpen => BadRequest(new { error = "not_open", message = "Регистрация закрыта" }),
        JoinTournamentError.Full => BadRequest(new { error = "full", message = "Турнир набрал максимум участников" }),
        JoinTournamentError.AlreadyJoined => BadRequest(new { error = "already_joined" }),
        _ => BadRequest(new { error = "bad_request" }),
    };

    private IActionResult MapLeaveError(LeaveTournamentError e) => e switch
    {
        LeaveTournamentError.TournamentNotFound => NotFound(new { error = "tournament_not_found" }),
        LeaveTournamentError.NotJoined => BadRequest(new { error = "not_joined" }),
        LeaveTournamentError.AlreadyStarted => BadRequest(new { error = "already_started", message = "Сетка уже построена — выйти нельзя" }),
        _ => BadRequest(new { error = "bad_request" }),
    };

    private IActionResult MapBracketError(GenerateBracketError e) => e switch
    {
        GenerateBracketError.TournamentNotFound => NotFound(new { error = "tournament_not_found" }),
        GenerateBracketError.NotCreator => StatusCode(403, new { error = "not_creator" }),
        GenerateBracketError.NotEnoughParticipants => BadRequest(new { error = "not_enough_participants", message = "Нужно минимум 2 участника" }),
        GenerateBracketError.AlreadyPlaying => BadRequest(new { error = "already_playing", message = "Сетку нельзя перестроить — уже есть сыгранные матчи" }),
        _ => BadRequest(new { error = "bad_request" }),
    };

    private IActionResult MapResultError(SetMatchResultError e) => e switch
    {
        SetMatchResultError.TournamentNotFound => NotFound(new { error = "tournament_not_found" }),
        SetMatchResultError.NotCreator => StatusCode(403, new { error = "not_creator" }),
        SetMatchResultError.MatchNotFound => NotFound(new { error = "match_not_found" }),
        SetMatchResultError.NotPlayable => BadRequest(new { error = "not_playable", message = "В матче ещё нет обоих соперников" }),
        SetMatchResultError.BadScore => BadRequest(new { error = "bad_score", message = "Счёт не соответствует формату матча" }),
        _ => BadRequest(new { error = "bad_request" }),
    };

    private IActionResult MapCancelError(CancelTournamentError e) => e switch
    {
        CancelTournamentError.TournamentNotFound => NotFound(new { error = "tournament_not_found" }),
        CancelTournamentError.NotAllowed => StatusCode(403, new { error = "not_allowed" }),
        CancelTournamentError.AlreadyFinished => BadRequest(new { error = "already_finished" }),
        _ => BadRequest(new { error = "bad_request" }),
    };
}
