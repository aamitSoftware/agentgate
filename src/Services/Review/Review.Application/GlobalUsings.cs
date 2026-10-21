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
global using Review.Domain.Grants;
global using Review.Domain.LocalCopies;
global using Review.Domain.ManifestReviews;
// Own layers
global using Review.Persistence;
global using Review.Persistence.Repositories;
