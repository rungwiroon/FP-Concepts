import * as E from 'fp-ts/Either';
import * as A from 'fp-ts/Apply';
import { pipe } from 'fp-ts/function';
import { getValidation } from 'fp-ts/Either';
import { getSemigroup } from 'fp-ts/Array';
import type { CreateTodoRequest } from './types';

export interface ValidationError {
  field: string;
  message: string;
}

export type ValidationResult<A> = E.Either<ValidationError[], A>;

const validateTitle = (title: string): ValidationResult<string> =>
  title.trim().length > 0 && title.length <= 200
    ? E.right(title)
    : E.left([{ 
        field: 'title', 
        message: 'Title is required and must be less than 200 characters' 
      }]);

const validateDescription = (description: string | null): ValidationResult<string | null> =>
  description === null || description.length <= 1000
    ? E.right(description)
    : E.left([{ 
        field: 'description', 
        message: 'Description must be less than 1000 characters' 
      }]);

const validationApplicative = getValidation(getSemigroup<ValidationError>());

export const validateTodo = (
  input: CreateTodoRequest
): ValidationResult<CreateTodoRequest> =>
  pipe(
    A.sequenceS(validationApplicative)({
      title: validateTitle(input.title),
      description: validateDescription(input.description),
    }),
    E.map(() => input)
  );
