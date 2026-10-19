using MediatR;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;

namespace NamViet.ERP.Modules.Warehouse.Application.Transactions;

// Chỉ khai báo commands ở Application – handlers nằm ở Infrastructure
public record CreateTransactionCommand(CreateTransactionRequest Request) : IRequest<TransactionDto>;
public record ApproveTransactionCommand(string TicketCode, bool Approve, string? Reason) : IRequest<string>;
