// Third-party libraries
global using System.Text.Json.Serialization;
global using FluentValidation;
global using MassTransit;
global using Mediator;
global using Microsoft.EntityFrameworkCore;
// Shared
global using AgentGate.Abstractions;
// Building blocks
global using Blocks.Core;
global using Blocks.Domain;
global using Blocks.Exceptions;
// Domain
global using Registry.Domain.Agents;
global using Registry.Domain.McpServers;
// Own layers
global using Registry.Application.Features.McpServers._Shared;
global using Registry.Persistence;
global using Registry.Persistence.Repositories;
