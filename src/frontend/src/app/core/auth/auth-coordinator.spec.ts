import { describe, expect, it } from 'vitest';
import { AuthCoordinator } from './auth-coordinator';

describe('single application refresh coordination', () => {
  it.each([false,true])('retains outstanding transport after invalidation (failure=%s)',async failure=>{
    const coordinator=new AuthCoordinator();let finish!:(value:number)=>void;let fail!:(error:Error)=>void;let calls=0;
    const pending=coordinator.run(()=>{calls++;return new Promise<number>((resolve,reject)=>{finish=resolve;fail=reject;});});
    const result=pending.catch(()=>undefined);coordinator.invalidate();let settled=false;const drain=coordinator.settle().then(()=>settled=true);
    const joined=coordinator.run(async()=>{calls++;return 2;}).catch(()=>undefined);
    await Promise.resolve();const settledEarly=settled;
    if(failure)fail(new Error('late failure'));else finish(1);
    await Promise.all([result,joined,drain]);expect(settledEarly).toBe(false);expect(calls).toBe(1);
    expect(await coordinator.run(async()=>3)).toBe(3);
  });
  it('joins one operation through completion and permits a later fresh operation', async () => {
    const coordinator = new AuthCoordinator(); let calls = 0; let finish!: (value: number) => void;
    const operation = () => { calls++; return new Promise<number>(resolve => finish = resolve); };
    const first = coordinator.run(operation); const second = coordinator.run(operation);
    expect(calls).toBe(1); finish(42); expect(await first).toBe(42); expect(await second).toBe(42);
    expect(await coordinator.run(async () => { calls++; return 43; })).toBe(43); expect(calls).toBe(2);
  });
  it('rejects all waiters on failure without retry and resets the operation', async () => {
    const coordinator = new AuthCoordinator(); let fail!: (reason: Error) => void; let calls = 0;
    const first = coordinator.run(() => { calls++; return new Promise<void>((_, reject) => fail = reject); });
    const second = coordinator.run(async () => { calls++; });
    const assertions = Promise.all([expect(first).rejects.toThrow('failed'), expect(second).rejects.toThrow('failed')]); fail(new Error('failed')); await assertions; expect(calls).toBe(1);
    await coordinator.run(async () => undefined);
  });
  it('invalidates stale operations without relying on browser locks', () => {
    const coordinator = new AuthCoordinator(); const before = coordinator.generation; coordinator.invalidate(); expect(coordinator.generation).toBe(before + 1);
  });
});
