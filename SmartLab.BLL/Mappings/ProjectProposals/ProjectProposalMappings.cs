using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.ProjectProposals;

public static class ProjectProposalMappings
{
    public static ProjectProposalResponse ToResponse(Project project)
    {
        var currentRequest = project.BorrowRequests
                                 .OrderByDescending(br => br.RequestDate)
                                 .ThenByDescending(br => br.BorrowRequestId)
                                 .FirstOrDefault()
                             ?? throw new InvalidOperationException("Project proposal has no borrow request");

        return new ProjectProposalResponse
        {
            ProjectId = project.ProjectId,
            TeamId = project.TeamId ?? Guid.Empty,
            ClassId = project.ClassId ?? Guid.Empty,
            Title = project.Title,
            Description = project.Description ?? string.Empty,
            ProjectStatus = project.Status,
            BorrowRequestId = currentRequest.BorrowRequestId,
            RequestCode = currentRequest.RequestCode,
            BorrowRequestStatus = currentRequest.Status,
            SubmittedAt = currentRequest.RequestDate,
            DueDate = currentRequest.DueDate,
            Components = project.ProjectComponents
                .OrderBy(pc => pc.Component.ComponentCode)
                .Select(pc => new ProposalComponentResponse
                {
                    ComponentId = pc.ComponentId,
                    ComponentCode = pc.Component.ComponentCode,
                    ComponentName = pc.Component.ComponentName,
                    Quantity = pc.Quantity,
                    ReservedQuantity = currentRequest.BorrowItems.Count(bi =>
                        bi.ComponentId == pc.ComponentId && bi.Status == BorrowItemStatuses.Reserved),
                })
                .ToList(),
        };
    }

    public static ProjectProposalReviewDetailResponse ToReviewDetailResponse(Project project)
    {
        var currentRequest = project.BorrowRequests
                                 .OrderByDescending(br => br.RequestDate)
                                 .ThenByDescending(br => br.BorrowRequestId)
                                 .FirstOrDefault()
                             ?? throw new InvalidOperationException("Project proposal has no borrow request");
        var team = project.Team ?? throw new InvalidOperationException("Project proposal has no team");
        var classInfo = project.Class ?? throw new InvalidOperationException("Project proposal has no class");

        return new ProjectProposalReviewDetailResponse
        {
            ProjectId = project.ProjectId,
            TeamId = team.TeamId,
            TeamName = team.TeamName,
            ClassId = classInfo.ClassId,
            ClassCode = classInfo.ClassCode,
            LeaderId = team.LeaderId,
            LeaderStudentCode = team.Leader.StudentCode,
            LeaderFullName = team.Leader.User.FullName,
            Title = project.Title,
            Description = project.Description ?? string.Empty,
            ProjectStatus = project.Status,
            BorrowRequestId = currentRequest.BorrowRequestId,
            RequestCode = currentRequest.RequestCode,
            BorrowRequestStatus = currentRequest.Status,
            RejectReason = currentRequest.RejectReason,
            SubmittedAt = currentRequest.RequestDate,
            DueDate = currentRequest.DueDate,
            Components = project.ProjectComponents
                .OrderBy(pc => pc.Component.ComponentCode)
                .Select(pc => new ProposalComponentResponse
                {
                    ComponentId = pc.ComponentId,
                    ComponentCode = pc.Component.ComponentCode,
                    ComponentName = pc.Component.ComponentName,
                    Quantity = pc.Quantity,
                    ReservedQuantity = currentRequest.BorrowItems.Count(bi =>
                        bi.ComponentId == pc.ComponentId && bi.Status == BorrowItemStatuses.Reserved),
                })
                .ToList(),
        };
    }
}
